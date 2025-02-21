/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NodaTime;
using RestSharp;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;
using QuantConnect.Lean.DataSource.ThetaData.Models.Common;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;
using System.Diagnostics.Contracts;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IHistoryProvider"/>
    /// </summary>
    public partial class ThetaDataProvider : SynchronizingHistoryProvider
    {
        /// <summary>
        /// Represents the time zone used by ThetaData, which returns time in the New York (EST) Time Zone with daylight savings time.
        /// </summary>
        /// <remarks>
        /// <see href="https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/ke230k18g7fld-trading-hours"/>
        /// </remarks>
        private static DateTimeZone TimeZoneThetaData = TimeZones.NewYork;

        /// <summary>
        /// Indicates whether the warning for invalid <see cref="SecurityType"/> has been fired.
        /// </summary>
        private volatile bool _invalidSecurityTypeWarningFired;

        /// <summary>
        /// Indicates whether the warning for invalid <see cref="ISubscriptionPlan.AccessibleResolutions"/> has been fired.
        /// </summary>
        private volatile bool _invalidSubscriptionResolutionRequestWarningFired;

        /// <summary>
        /// Indicates whether the warning indicating that the requested date is greater than the <see cref="ISubscriptionPlan.FirstAccessDate"/> has been triggered.
        /// </summary>
        private volatile bool _invalidStartDateInCurrentSubscriptionWarningFired;

        /// <summary>
        /// Indicates whether a warning for an invalid start time has been fired, where the start time is greater than or equal to the end time in UTC.
        /// </summary>
        private volatile bool _invalidStartTimeWarningFired;

        /// <summary>
        /// Indicates whether a warning has been triggered for an invalid TickType request for Index securities.
        /// </summary>
        /// <remarks>
        /// This flag is set to true when an invalid TickType request is made for Index securities. 
        /// Specifically, only 'Trade' TickType is supported for Index securities, and this warning helps to prevent invalid requests.
        /// </remarks>
        private volatile bool _invalidIndexTickTypeWarningFired;

        /// <inheritdoc />
        public override void Initialize(HistoryProviderInitializeParameters parameters)
        { }

        /// <inheritdoc />
        public override IEnumerable<Slice>? GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            var subscriptions = new List<Subscription>();
            foreach (var request in requests)
            {
                var history = GetHistory(request);

                var subscription = CreateSubscription(request, history);
                if (!subscription.MoveNext())
                {
                    continue;
                }

                subscriptions.Add(subscription);
            }

            if (subscriptions.Count == 0)
            {
                return null;
            }
            return CreateSliceEnumerableFromSubscriptions(subscriptions, sliceTimeZone);
        }

        public IEnumerable<BaseData>? GetHistory(HistoryRequest historyRequest)
        {
            if (!_userSubscriptionPlan.AccessibleResolutions.Contains(historyRequest.Resolution))
            {
                if (!_invalidSubscriptionResolutionRequestWarningFired)
                {
                    _invalidSubscriptionResolutionRequestWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: The current user's subscription plan does not support the requested resolution: {historyRequest.Resolution}");
                }
                return null;
            }

            var startDateTimeUtc = historyRequest.StartTimeUtc;
            if (_userSubscriptionPlan.FirstAccessDate.Date > historyRequest.StartTimeUtc.Date)
            {
                if (!_invalidStartDateInCurrentSubscriptionWarningFired)
                {
                    _invalidStartDateInCurrentSubscriptionWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: The requested start time ({historyRequest.StartTimeUtc.Date}) exceeds the maximum available date ({_userSubscriptionPlan.FirstAccessDate.Date}) allowed by the user's subscription. Using the new adjusted start date: {_userSubscriptionPlan.FirstAccessDate.Date}.");
                }
                // Ensures efficient data retrieval by blocking requests outside the user's subscription period, which reduces processing overhead and avoids unnecessary data requests.
                startDateTimeUtc = _userSubscriptionPlan.FirstAccessDate.Date;

                if (startDateTimeUtc >= historyRequest.EndTimeUtc)
                {
                    return null;
                }
            }

            if (!CanSubscribe(historyRequest.Symbol))
            {
                if (!_invalidSecurityTypeWarningFired)
                {
                    _invalidSecurityTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Unsupported SecurityType '{historyRequest.Symbol.SecurityType}' for symbol '{historyRequest.Symbol}'");
                }
                return null;
            }

            if (startDateTimeUtc >= historyRequest.EndTimeUtc)
            {
                if (!_invalidStartTimeWarningFired)
                {
                    _invalidStartTimeWarningFired = true;
                    Log.Error($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Error - The start date in the history request must come before the end date. No historical data will be returned.");
                }
                return null;
            }

            if (historyRequest.Symbol.SecurityType == SecurityType.Index && historyRequest.TickType != TickType.Trade)
            {
                if (!_invalidIndexTickTypeWarningFired)
                {
                    _invalidIndexTickTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Request error: For Index securities, only 'Trade' TickType is supported.You requested '{historyRequest.TickType}'.");
                }
                return null;
            }

            var restRequest = new RestRequest(Method.GET);

            restRequest = GetSymbolHistoryQueryParametersBySymbol(restRequest, historyRequest.Symbol);

            var startDateTimeLocal = startDateTimeUtc.ConvertFromUtc(TimeZoneThetaData);
            var endDateTimeLocal = historyRequest.EndTimeUtc.ConvertFromUtc(TimeZoneThetaData);

            restRequest.AddQueryParameter(RequestParameters.StartDate, startDateTimeLocal.ConvertToThetaDataDateFormat());
            restRequest.AddQueryParameter(RequestParameters.EndDate, endDateTimeLocal.ConvertToThetaDataDateFormat());
            restRequest.AddOrUpdateParameter("start_time", "0", ParameterType.QueryString);

            restRequest.Resource = GetResourceUrlHistoryData(historyRequest.Symbol.SecurityType, historyRequest.TickType, historyRequest.Resolution);

            var symbolExchangeTimeZone = historyRequest.Symbol.GetSymbolExchangeTimeZone();

            if (historyRequest.Symbol.SecurityType == SecurityType.Index && historyRequest.Resolution <= Resolution.Hour)
            {
                return GetIndexIntradayHistoryData(restRequest, historyRequest.Symbol, historyRequest.Resolution, symbolExchangeTimeZone);
            }
            else if (historyRequest.TickType == TickType.OpenInterest)
            {
                return GetHistoricalOpenInterestData(restRequest, historyRequest.Symbol, symbolExchangeTimeZone);
            }

            var history = default(IEnumerable<BaseData>);
            switch (historyRequest.Resolution)
            {
                case Resolution.Tick:
                    history = GetTickHistoryData(restRequest, historyRequest.Symbol, Resolution.Tick, historyRequest.TickType, startDateTimeLocal, historyRequest.EndTimeUtc, symbolExchangeTimeZone);
                    break;
                case Resolution.Second:
                case Resolution.Minute:
                case Resolution.Hour:
                    history = GetIntradayHistoryData(restRequest, historyRequest.Symbol, historyRequest.Resolution, historyRequest.TickType, symbolExchangeTimeZone);
                    break;
                case Resolution.Daily:
                    history = GetDailyHistoryData(restRequest, historyRequest.Symbol, historyRequest.Resolution, historyRequest.TickType, symbolExchangeTimeZone);
                    break;
                default:
                    throw new ArgumentException($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Invalid resolution: {historyRequest.Resolution}. Supported resolutions are Tick, Second, Minute, Hour, and Daily.");
            }

            return FilterHistory(history, historyRequest, startDateTimeLocal, endDateTimeLocal);
        }

        private IEnumerable<BaseData> FilterHistory(IEnumerable<BaseData> history, HistoryRequest request, DateTime startTimeLocal, DateTime endTimeLocal)
        {
            Log.Trace($"FilterHistory: startTimeLocal = {startTimeLocal}, endTimeLocal = {endTimeLocal}");
            // cleaning the data before returning it back to user
            foreach (var bar in history)
            {
                Log.Trace($"Income: Time = {bar.Time}, EndTime = {bar.Time}");
                if (bar.Time >= startTimeLocal && bar.EndTime <= endTimeLocal)
                {
                    if (request.ExchangeHours.IsOpen(bar.Time, bar.EndTime, request.IncludeExtendedMarketHours))
                    {
                        yield return bar;
                    }
                }
            }

            Log.Trace($"InteractiveBrokersBrokerage::GetHistory(): Download completed: {request.Symbol.Value}");
        }

        public IEnumerable<BaseData>? GetIndexIntradayHistoryData(RestRequest request, Symbol symbol, Resolution resolution, DateTimeZone symbolExchangeTimeZone)
        {
            request.AddQueryParameter(RequestParameters.IntervalInMilliseconds, GetIntervalsInMilliseconds(resolution));

            var period = resolution.ToTimeSpan();
            foreach (var prices in _restApiClient.ExecuteRequest<BaseResponse<PriceResponse>>(request))
            {
                if (resolution == Resolution.Tick)
                {
                    foreach (var price in prices.Response)
                    {
                        if (price.Price != 0m)
                        {
                            yield return new Tick(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(price.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, "", "", 0m, price.Price);
                        }
                    }
                }
                else
                {
                    foreach (var price in prices.Response)
                    {
                        if (price.Price != 0m)
                        {
                            yield return new TradeBar(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(price.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, price.Price, price.Price, price.Price, price.Price, 0m, period);
                        }
                    }
                }
            }
        }

        public IEnumerable<BaseData>? GetTickHistoryData(RestRequest request, Symbol symbol, Resolution resolution, TickType tickType, DateTime startDateTimeUtc, DateTime endDateTimeUtc, DateTimeZone symbolExchangeTimeZone)
        {
            switch (tickType)
            {
                case TickType.Trade:
                    return GetHistoricalTickTradeDataByOneDayInterval(request, symbol, startDateTimeUtc, endDateTimeUtc, symbolExchangeTimeZone);
                case TickType.Quote:
                    request.AddQueryParameter(RequestParameters.IntervalInMilliseconds, GetIntervalsInMilliseconds(resolution));

                    Func<QuoteResponse, BaseData> quoteCallback =
                        (quote) => new Tick(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(quote.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, quote.AskCondition, quote.AskExchange.TryGetExchangeOrDefault(), quote.BidSize, quote.BidPrice, quote.AskSize, quote.AskPrice);

                    return GetHistoricalQuoteData(request, quoteCallback);
                default:
                    throw new ArgumentException($"Invalid tick type: {tickType}.");
            }
        }

        public IEnumerable<BaseData>? GetIntradayHistoryData(RestRequest request, Symbol symbol, Resolution resolution, TickType tickType, DateTimeZone symbolExchangeTimeZone)
        {
            request.AddQueryParameter(RequestParameters.IntervalInMilliseconds, GetIntervalsInMilliseconds(resolution));

            var period = resolution.ToTimeSpan();

            switch (tickType)
            {
                case TickType.Trade:
                    return GetHistoricalOpenHighLowCloseData(request, symbol, period, symbolExchangeTimeZone);
                case TickType.Quote:
                    Func<QuoteResponse, BaseData> quoteCallback =
                        (quote) =>
                        {
                            var bar = new QuoteBar(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(quote.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, null, decimal.Zero, null, decimal.Zero, period);
                            bar.UpdateQuote(quote.BidPrice, quote.BidSize, quote.AskPrice, quote.AskSize);
                            return bar;
                        };

                    return GetHistoricalQuoteData(request, quoteCallback);
                default:
                    throw new ArgumentException($"Invalid tick type: {tickType}.");
            }
        }

        public IEnumerable<BaseData>? GetDailyHistoryData(RestRequest request, Symbol symbol, Resolution resolution, TickType tickType, DateTimeZone symbolExchangeTimeZone)
        {
            var period = resolution.ToTimeSpan();
            switch (tickType)
            {
                case TickType.Trade:
                    return GetHistoryEndOfDay(request,
                        // If OHLC prices zero, low trading activity, empty result, low volatility.
                        (eof) => eof.Open == 0 || eof.High == 0 || eof.Low == 0 || eof.Close == 0,
                        (tradeDateTime, eof) => new TradeBar(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(tradeDateTime.Date, symbolExchangeTimeZone), symbol, eof.Open, eof.High, eof.Low, eof.Close, eof.Volume, period));
                case TickType.Quote:
                    return GetHistoryEndOfDay(request,
                        // If Ask/Bid - prices/sizes zero, low quote activity, empty result, low volatility.
                        (eof) => eof.AskPrice == 0 || eof.AskSize == 0 || eof.BidPrice == 0 || eof.BidSize == 0,
                        (quoteDateTime, eof) =>
                        {
                            var bar = new QuoteBar(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(quoteDateTime.Date, symbolExchangeTimeZone), symbol, null, decimal.Zero, null, decimal.Zero, period);
                            bar.UpdateQuote(eof.BidPrice, eof.BidSize, eof.AskPrice, eof.AskSize);
                            return bar;
                        });
                default:
                    throw new ArgumentException($"Invalid tick type: {tickType}.");
            }
        }

        private IEnumerable<BaseData> GetHistoricalOpenInterestData(RestRequest request, Symbol symbol, DateTimeZone symbolExchangeTimeZone)
        {
            foreach (var openInterests in _restApiClient.ExecuteRequest<BaseResponse<OpenInterestResponse>>(request))
            {
                foreach (var openInterest in openInterests.Response)
                {
                    yield return new OpenInterest(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(openInterest.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, openInterest.OpenInterest);
                }
            }
        }

        private IEnumerable<Tick> GetHistoricalTickTradeDataByOneDayInterval(RestRequest request, Symbol symbol, DateTime startDateTimeUtc, DateTime endDateTimeUtc, DateTimeZone symbolExchangeTimeZone)
        {
            var startDateTimeET = startDateTimeUtc.ConvertFromUtc(TimeZoneThetaData);
            var endDateTimeET = endDateTimeUtc.ConvertFromUtc(TimeZoneThetaData);

            foreach (var dateRange in ThetaDataExtensions.GenerateDateRangesWithInterval(startDateTimeET, endDateTimeET))
            {
                request.AddOrUpdateParameter(RequestParameters.StartDate, dateRange.startDate.ConvertToThetaDataDateFormat(), ParameterType.QueryString);
                request.AddOrUpdateParameter(RequestParameters.EndDate, dateRange.endDate.ConvertToThetaDataDateFormat(), ParameterType.QueryString);

                foreach (var trades in _restApiClient.ExecuteRequest<BaseResponse<TradeResponse>>(request))
                {
                    foreach (var trade in trades.Response)
                    {
                        yield return new Tick(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(trade.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, trade.Condition.ToStringInvariant(), trade.Exchange.TryGetExchangeOrDefault(), trade.Size, trade.Price);
                    }
                }
            }
        }

        private IEnumerable<TradeBar> GetHistoricalOpenHighLowCloseData(RestRequest request, Symbol symbol, TimeSpan period, DateTimeZone symbolExchangeTimeZone)
        {
            foreach (var trades in _restApiClient.ExecuteRequest<BaseResponse<OpenHighLowCloseResponse>>(request))
            {
                foreach (var trade in trades.Response)
                {
                    // If Open|High|Low|Close - prices zero, low trade activity, empty result, low volatility.
                    if (trade.Open == 0 || trade.High == 0 || trade.Low == 0 || trade.Close == 0)
                    {
                        continue;
                    }

                    yield return new TradeBar(ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(trade.DateTimeMilliseconds, symbolExchangeTimeZone), symbol, trade.Open, trade.High, trade.Low, trade.Close, trade.Volume, period);
                }
            }
        }

        private IEnumerable<BaseData> GetHistoricalQuoteData(RestRequest request, Func<QuoteResponse, BaseData> callback)
        {
            foreach (var quotes in _restApiClient.ExecuteRequest<BaseResponse<QuoteResponse>>(request))
            {
                foreach (var quote in quotes.Response)
                {
                    // If Ask/Bid - prices/sizes zero, low quote activity, empty result, low volatility.
                    if (quote.AskPrice == 0 || quote.AskSize == 0 || quote.BidPrice == 0 || quote.BidSize == 0)
                    {
                        continue;
                    }

                    yield return callback(quote);
                }
            }
        }

        private IEnumerable<BaseData>? GetHistoryEndOfDay(RestRequest request, Func<EndOfDayReportResponse, bool> validateEmptyResponse, Func<DateTime, EndOfDayReportResponse, BaseData> res)
        {
            foreach (var endOfDays in _restApiClient.ExecuteRequest<BaseResponse<EndOfDayReportResponse>>(request))
            {
                foreach (var endOfDay in endOfDays.Response)
                {
                    if (validateEmptyResponse(endOfDay))
                    {
                        continue;
                    }
                    yield return res(endOfDay.LastTradeDateTimeMilliseconds, endOfDay);
                }
            }
        }

        /// <summary>
        /// Returns the interval in milliseconds corresponding to the specified resolution.
        /// </summary>
        /// <param name="resolution">The <see cref="Resolution"/> for which to retrieve the interval.</param>
        /// <returns>
        /// The interval in milliseconds as a string. 
        /// For <see cref="Resolution.Tick"/>, returns "0".
        /// For <see cref="Resolution.Second"/>, returns "1000".
        /// For <see cref="Resolution.Minute"/>, returns "60000".
        /// For <see cref="Resolution.Hour"/>, returns "3600000".
        /// </returns>
        /// <exception cref="NotSupportedException">Thrown when the specified resolution is not supported.</exception>
        private string GetIntervalsInMilliseconds(Resolution resolution) => resolution switch
        {
            Resolution.Tick => "0",
            Resolution.Second => "1000",
            Resolution.Minute => "60000",
            Resolution.Hour => "3600000",
            _ => throw new NotSupportedException($"{nameof(ThetaDataProvider)}.{nameof(GetIntervalsInMilliseconds)}: The resolution type '{resolution}' is not supported.")
        };

        /// <summary>
        /// Adds query parameters to the provided <see cref="RestRequest"/> based on the given <see cref="Symbol"/>.
        /// </summary>
        /// <param name="request">The <see cref="RestRequest"/> to which query parameters will be added.</param>
        /// <param name="symbol">The <see cref="Symbol"/> containing the security type and ticker information.</param>
        /// <returns>The updated <see cref="RestRequest"/> with added query parameters.</returns>
        /// <exception cref="NotImplementedException">
        /// Thrown when the security type of the provided symbol is not implemented.
        /// </exception>
        private RestRequest GetSymbolHistoryQueryParametersBySymbol(RestRequest request, Symbol symbol)
        {
            var ticker = _symbolMapper.GetBrokerageSymbol(symbol);

            switch (symbol.SecurityType)
            {
                case SecurityType.Index:
                case SecurityType.Equity:
                    request.AddQueryParameter("root", ticker);
                    break;
                case SecurityType.Option:
                case SecurityType.IndexOption:
                    var tickerOption = ticker.Split(',');
                    request.AddQueryParameter("root", tickerOption[0]);
                    request.AddQueryParameter("exp", tickerOption[1]);
                    request.AddQueryParameter("strike", tickerOption[2]);
                    request.AddQueryParameter("right", tickerOption[3]);
                    break;
                default:
                    throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetSymbolHistoryQueryParametersBySymbol)}: Security type '{symbol.SecurityType}' is not implemented.");
            }

            return request;
        }

        /// <summary>
        /// Retrieves the resource URL for historical tick data based on security type, tick type, and resolution.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <param name="tickType">The type of tick data (e.g., Trade, Quote, OpenInterest).</param>
        /// <param name="resolution">The resolution of the data (e.g., Tick, Second, Minute, Hour, Daily).</param>
        /// <returns>The resource URL for the requested historical tick data.</returns>
        /// <exception cref="ArgumentException">Thrown when an invalid tick type is provided.</exception>
        private string GetResourceUrlHistoryData(SecurityType securityType, TickType tickType, Resolution resolution)
        {
            return tickType switch
            {
                TickType.Trade => GetTradeResourceUrl(securityType, resolution),
                TickType.Quote => GetQuoteResourceUrl(securityType, resolution),
                TickType.OpenInterest when securityType == SecurityType.Option => "/hist/option/open_interest",
                _ => throw new ArgumentException($"{nameof(ThetaDataProvider)}.{nameof(GetResourceUrlHistoryData)}: Invalid tick type: {tickType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for trade tick data based on security type and resolution.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <param name="resolution">The resolution of the data (e.g., Tick, Second, Minute, Hour, Daily).</param>
        /// <returns>The resource URL for trade tick data.</returns>
        /// <exception cref="NotImplementedException">Thrown when the resolution is not implemented for trade tick data.</exception>
        private string GetTradeResourceUrl(SecurityType securityType, Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Tick => GetTradeTickResourceUrl(securityType),
                Resolution.Second or Resolution.Minute or Resolution.Hour => GetTradeIntradayResourceUrl(securityType),
                Resolution.Daily => GetTradeDailyResourceUrl(securityType),
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetTradeResourceUrl)}: Resolution not implemented for trade: {resolution}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for trade tick data at tick resolution based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <returns>The resource URL for trade tick data at tick resolution.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for trade tick data at tick resolution.</exception>
        private string GetTradeTickResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Index => "/hist/index/price",
                SecurityType.Equity => "/hist/stock/trade",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/trade",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetTradeTickResourceUrl)}: Trade tick resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for trade tick data at intraday resolutions based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <returns>The resource URL for trade tick data at intraday resolutions.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for trade tick data at intraday resolutions.</exception>
        private string GetTradeIntradayResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Index => "/hist/index/price",
                SecurityType.Equity => "/hist/stock/ohlc",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/ohlc",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetTradeIntradayResourceUrl)}: Trade intraday resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for trade tick data at daily resolution based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <returns>The resource URL for trade tick data at daily resolution.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for trade tick data at daily resolution.</exception>
        private string GetTradeDailyResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Index => "/hist/index/eod",
                SecurityType.Equity => "/hist/stock/eod",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/eod",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetTradeDailyResourceUrl)}: Trade daily resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for quote tick data based on security type and resolution.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <param name="resolution">The resolution of the data (e.g., Tick, Second, Minute, Hour, Daily).</param>
        /// <returns>The resource URL for quote tick data.</returns>
        /// <exception cref="NotImplementedException">Thrown when the resolution is not implemented for quote tick data.</exception>
        private string GetQuoteResourceUrl(SecurityType securityType, Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Tick => GetQuoteTickResourceUrl(securityType),
                Resolution.Second or Resolution.Minute or Resolution.Hour => GetQuoteIntradayResourceUrl(securityType),
                Resolution.Daily => GetQuoteDailyResourceUrl(securityType),
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetQuoteResourceUrl)}: Resolution not implemented for quote: {resolution}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for quote tick data at tick resolution based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option).</param>
        /// <returns>The resource URL for quote tick data at tick resolution.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for quote tick data at tick resolution.</exception>
        private string GetQuoteTickResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Equity => "/hist/stock/quote",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/quote",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetQuoteTickResourceUrl)}: Quote tick resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for quote tick data at intraday resolutions based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option, Index).</param>
        /// <returns>The resource URL for quote tick data at intraday resolutions.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for quote tick data at intraday resolutions.</exception>
        private string GetQuoteIntradayResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Index => "/hist/index/price",
                SecurityType.Equity => "/hist/stock/quote",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/quote",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetQuoteIntradayResourceUrl)}: Quote intraday resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Retrieves the resource URL for quote tick data at daily resolution based on security type.
        /// </summary>
        /// <param name="securityType">The type of security (e.g., Equity, Option).</param>
        /// <returns>The resource URL for quote tick data at daily resolution.</returns>
        /// <exception cref="NotImplementedException">Thrown when the security type is not implemented for quote tick data at daily resolution.</exception>
        private string GetQuoteDailyResourceUrl(SecurityType securityType)
        {
            return securityType switch
            {
                SecurityType.Equity => "/hist/stock/eod",
                SecurityType.IndexOption or SecurityType.Option => "/hist/option/eod",
                _ => throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetQuoteDailyResourceUrl)}: Quote daily resource URL not implemented for security type: {securityType}.")
            };
        }

        /// <summary>
        /// Converts the given <paramref name="thetaDataDateTime"/> from the ThetaData time zone to the specified symbol exchange time zone.
        /// </summary>
        /// <param name="thetaDataDateTime">The date and time in the ThetaData time zone returned by the API in a history request.</param>
        /// <param name="symbolExchangeDateTimeZone">The destination time zone representing the symbol's exchange time zone.</param>
        /// <returns>A <see cref="DateTime"/> value representing the given <paramref name="thetaDataDateTime"/> in the specified symbol exchange time zone.</returns>
        /// <remarks>
        /// The ThetaData time zone is New York (EST) Time Zone with daylight savings time.
        /// For more information, see <see href="https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/ke230k18g7fld-trading-hours"/>.
        /// </remarks>
        private static DateTime ConvertThetaDataTimeZoneToSymbolExchangeTimeZone(DateTime thetaDataDateTime, DateTimeZone symbolExchangeDateTimeZone)
            => thetaDataDateTime.ConvertTo(TimeZoneThetaData, symbolExchangeDateTimeZone);
    }
}