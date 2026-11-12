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
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using System.Collections.Concurrent;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;

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
        private static readonly DateTimeZone TimeZoneThetaData = ThetaDataBarConverter.ThetaDataTimeZone;

        /// <summary>
        /// Loads the end-of-day rows of a whole option chain in 1 request.
        /// </summary>
        private ThetaDataOptionEndOfDayLoader _optionEndOfDayLoader;

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
        /// Only 'Trade' TickType is supported for Index securities, and this warning helps to prevent invalid requests.
        /// </remarks>
        private volatile bool _invalidIndexTickTypeWarningFired;

        /// <summary>
        /// The endpoints the plan denied, each logged once.
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _entitlementWarningsFired = new();

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

        /// <summary>
        /// Gets the history of 1 request.
        /// </summary>
        /// <returns>The data, or null when the request is not supported or ThetaData has no data the plan allows.</returns>
        /// <exception cref="ThetaDataRequestException">ThetaData failed to answer, for a reason other than the plan.</exception>
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
            // The first date of the plan is an ET date. As UTC midnight it would be the evening before in ET,
            // the request would ask for that day, and ThetaData would deny the whole range (HTTP 403).
            var firstAccessUtc = _userSubscriptionPlan.FirstAccessDate.Date.ConvertToUtc(TimeZoneThetaData);
            if (firstAccessUtc > historyRequest.StartTimeUtc)
            {
                if (!_invalidStartDateInCurrentSubscriptionWarningFired)
                {
                    _invalidStartDateInCurrentSubscriptionWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: The requested start time ({historyRequest.StartTimeUtc.Date}) is before the first date ({_userSubscriptionPlan.FirstAccessDate.Date}) that the user's subscription allows. Using the new adjusted start date: {_userSubscriptionPlan.FirstAccessDate.Date}.");
                }
                // Ensures efficient data retrieval by blocking requests outside the user's subscription period, which reduces processing overhead and avoids unnecessary data requests.
                startDateTimeUtc = firstAccessUtc;
                if (startDateTimeUtc >= historyRequest.EndTimeUtc)
                {
                    return null;
                }
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

            if (!CanSubscribe(historyRequest.Symbol))
            {
                if (!_invalidSecurityTypeWarningFired)
                {
                    _invalidSecurityTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Unsupported SecurityType '{historyRequest.Symbol.SecurityType}' for symbol '{historyRequest.Symbol}'");
                }
                return null;
            }

            if (historyRequest.Symbol.SecurityType == SecurityType.Index && historyRequest.TickType != TickType.Trade)
            {
                if (!_invalidIndexTickTypeWarningFired)
                {
                    _invalidIndexTickTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Request error: For Index securities, only 'Trade' TickType is supported. You requested '{historyRequest.TickType}'.");
                }
                return null;
            }

            if (historyRequest.TickType == TickType.OpenInterest && !historyRequest.Symbol.SecurityType.IsOption())
            {
                return null;
            }

            var history = WhenEntitled(historyRequest.Symbol.SecurityType, historyRequest.TickType, () => GetContractHistory(historyRequest, startDateTimeUtc));
            if (history == null)
            {
                return null;
            }

            var exchangeTimeZone = historyRequest.ExchangeHours.TimeZone;
            return FilterHistory(ThetaDataBarConverter.OrderAndRemoveDuplicates(history, historyRequest.Resolution), historyRequest,
                startDateTimeUtc.ConvertFromUtc(exchangeTimeZone), historyRequest.EndTimeUtc.ConvertFromUtc(exchangeTimeZone));
        }

        /// <summary>
        /// Sends the request for 1 stock, index, or option contract, and converts the rows.
        /// </summary>
        private List<BaseData> GetContractHistory(HistoryRequest historyRequest, DateTime startDateTimeUtc)
        {
            var symbol = historyRequest.Symbol;
            var securityType = symbol.SecurityType;
            var resolution = historyRequest.Resolution;
            var exchangeTimeZone = historyRequest.ExchangeHours.TimeZone;

            var queryParameters = GetSymbolQueryParameters(symbol);
            queryParameters[RequestParameters.StartDate] = startDateTimeUtc.ConvertFromUtc(TimeZoneThetaData).ConvertToThetaDataDateFormat();
            // The end time is exclusive. A request that ends at midnight holds no data of that day, so it ends on the day before.
            var endDateTimeThetaData = historyRequest.EndTimeUtc.ConvertFromUtc(TimeZoneThetaData);
            var lastDate = endDateTimeThetaData.TimeOfDay == TimeSpan.Zero ? endDateTimeThetaData.AddDays(-1) : endDateTimeThetaData;
            queryParameters[RequestParameters.EndDate] = lastDate.ConvertToThetaDataDateFormat();

            if (historyRequest.TickType == TickType.OpenInterest)
            {
                return _restApiClient.GetRows<OpenInterestRow>(ThetaDataEndpoints.OptionOpenInterest, queryParameters)
                    .Select(row => (BaseData)ThetaDataBarConverter.ToOpenInterest(row, symbol, exchangeTimeZone)).ToList();
            }

            if (resolution == Resolution.Daily)
            {
                var rows = _restApiClient.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(securityType), queryParameters);
                return historyRequest.TickType == TickType.Quote
                    ? NotNull(rows.Select(row => ThetaDataBarConverter.ToDailyQuoteBar(row, symbol, exchangeTimeZone)))
                    : NotNull(rows.Select(row => ThetaDataBarConverter.ToDailyTradeBar(row, symbol, exchangeTimeZone)));
            }

            SetSessionTimes(queryParameters, securityType, historyRequest.IncludeExtendedMarketHours);
            var period = resolution.ToTimeSpan();

            if (resolution == Resolution.Tick && historyRequest.TickType == TickType.Trade && securityType != SecurityType.Index)
            {
                // The trade endpoint takes no interval, so the client cannot split the date range: request 1 day at a time.
                var ticks = new List<BaseData>();
                foreach (var (dayStart, dayEnd) in ThetaDataExtensions.GenerateDateRangesWithInterval(
                    queryParameters[RequestParameters.StartDate].ConvertFromThetaDataDateFormat(), queryParameters[RequestParameters.EndDate].ConvertFromThetaDataDateFormat(), 1))
                {
                    queryParameters[RequestParameters.StartDate] = dayStart.ConvertToThetaDataDateFormat();
                    queryParameters[RequestParameters.EndDate] = dayEnd.ConvertToThetaDataDateFormat();
                    ticks.AddRange(NotNull(_restApiClient.GetRows<TradeRow>(ThetaDataEndpoints.Trade(securityType), queryParameters)
                        .Select(row => ThetaDataBarConverter.ToTradeTick(row, symbol, exchangeTimeZone))));
                }
                return ticks;
            }

            queryParameters[RequestParameters.Interval] = ThetaDataEndpoints.GetInterval(resolution);
            switch (historyRequest.TickType)
            {
                case TickType.Trade when securityType == SecurityType.Index && resolution == Resolution.Tick:
                    return NotNull(_restApiClient.GetRows<PriceRow>(ThetaDataEndpoints.Trade(securityType), queryParameters)
                        .Select(row => ThetaDataBarConverter.ToIndexTick(row, symbol, exchangeTimeZone)));
                case TickType.Trade:
                    return NotNull(_restApiClient.GetRows<OhlcRow>(ThetaDataEndpoints.Ohlc(securityType), queryParameters)
                        .Select(row => ThetaDataBarConverter.ToTradeBar(row, symbol, period, exchangeTimeZone)));
                case TickType.Quote when resolution == Resolution.Tick:
                    return NotNull(_restApiClient.GetRows<QuoteRow>(ThetaDataEndpoints.Quote(securityType), queryParameters)
                        .Select(row => ThetaDataBarConverter.ToQuoteTick(row, symbol, exchangeTimeZone)));
                case TickType.Quote:
                    return NotNull(_restApiClient.GetRows<QuoteRow>(ThetaDataEndpoints.Quote(securityType), queryParameters)
                        .Select(row => ThetaDataBarConverter.ToQuoteBar(row, symbol, period, exchangeTimeZone)));
                default:
                    throw new ArgumentException($"{nameof(ThetaDataProvider)}.{nameof(GetContractHistory)}: Invalid tick type: {historyRequest.TickType}.");
            }
        }

        /// <summary>
        /// Runs a request. When the plan does not include the data, it logs this once for the endpoint and returns null.
        /// Any other failure still throws, so a failed request never looks like an empty result.
        /// </summary>
        private T? WhenEntitled<T>(SecurityType securityType, TickType tickType, Func<T> request) where T : class
        {
            try
            {
                return request();
            }
            catch (ThetaDataRequestException e) when (e.IsEntitlementError)
            {
                if (_entitlementWarningsFired.TryAdd($"{securityType}-{tickType}", true))
                {
                    Log.Error($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Your ThetaData plan does not include {securityType} {tickType} data, so no data is returned for it. {e.Message}");
                }
                return null;
            }
        }

        private IEnumerable<BaseData> FilterHistory(IEnumerable<BaseData> history, HistoryRequest request, DateTime startTimeLocal, DateTime endTimeLocal)
        {
            // cleaning the data before returning it back to user
            foreach (var bar in history)
            {
                if (request.Resolution == Resolution.Daily)
                {
                    if (IsDailyBarInRange(bar.Time, request.ExchangeHours, startTimeLocal, endTimeLocal))
                    {
                        yield return bar;
                    }
                    continue;
                }

                if (bar.Time >= startTimeLocal && bar.EndTime <= endTimeLocal)
                {
                    if (request.ExchangeHours.IsOpen(bar.Time, bar.EndTime, request.IncludeExtendedMarketHours))
                    {
                        yield return bar;
                    }
                }
            }
        }

        /// <summary>
        /// True when the market close of <paramref name="day"/> is after <paramref name="startLocal"/> and not after <paramref name="endLocal"/>.
        /// </summary>
        /// <remarks>
        /// LEAN treats a daily bar as ending at the market close (the default <c>daily-precise-end-time</c>), not at the midnight after it.
        /// <c>lean data download</c> passes UTC midnights, for example 2026-10-20 00:00 UTC to 2026-10-21 00:00 UTC, which is
        /// 2026-10-19 20:00 to 2026-10-20 20:00 ET. That request must return the bar of 2026-10-20, whose close is 16:00 ET.
        /// </remarks>
        private static bool IsDailyBarInRange(DateTime day, SecurityExchangeHours exchangeHours, DateTime startLocal, DateTime endLocal)
        {
            // An always-open market has no close, so its day ends at midnight.
            var close = exchangeHours.IsMarketAlwaysOpen ? day.Date.AddDays(1) : exchangeHours.GetNextMarketClose(day.Date, false);
            return close.Date <= day.Date.AddDays(1) && close > startLocal && close <= endLocal;
        }

        /// <summary>
        /// Gets the v3 query parameters that select a stock, an index, or an option contract.
        /// </summary>
        private Dictionary<string, string> GetSymbolQueryParameters(Symbol symbol)
        {
            switch (symbol.SecurityType)
            {
                case SecurityType.Index:
                case SecurityType.Equity:
                    return new Dictionary<string, string> { [RequestParameters.Symbol] = _symbolMapper.GetBrokerageSymbol(symbol) };
                case SecurityType.Option:
                case SecurityType.IndexOption:
                    return _symbolMapper.GetOptionContractQueryParameters(symbol);
                default:
                    throw new NotImplementedException($"{nameof(ThetaDataProvider)}.{nameof(GetSymbolQueryParameters)}: Security type '{symbol.SecurityType}' is not implemented.");
            }
        }

        /// <summary>
        /// Sets the time of day of an intraday request. The v3 default is 09:30 to 16:00 ET.
        /// </summary>
        /// <remarks>
        /// For options and indexes the first time is 09:00, so 1-hour bars start on the hour, as LEAN hour bars do.
        /// No option trades before 09:30, so the 09:00 bar holds only regular-session data.
        /// Index options trade until 16:15 ET, so options and indexes end at 16:15.
        /// </remarks>
        private static void SetSessionTimes(Dictionary<string, string> queryParameters, SecurityType securityType, bool includeExtendedMarketHours)
        {
            if (securityType == SecurityType.Equity && includeExtendedMarketHours)
            {
                queryParameters[RequestParameters.StartTime] = "04:00:00";
                queryParameters[RequestParameters.EndTime] = "20:00:00";
            }
            else if (securityType == SecurityType.Equity)
            {
                // 09:30, not 09:00: a 09:00 hour bar would hold pre-market trades.
                queryParameters[RequestParameters.StartTime] = "09:30:00";
                queryParameters[RequestParameters.EndTime] = "16:00:00";
            }
            else
            {
                queryParameters[RequestParameters.StartTime] = "09:00:00";
                queryParameters[RequestParameters.EndTime] = "16:15:00";
            }
        }

        private static List<BaseData> NotNull(IEnumerable<BaseData?> data) => data.Where(x => x != null).Select(x => x!).ToList();
    }
}
