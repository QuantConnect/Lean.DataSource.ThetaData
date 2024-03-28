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

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IHistoryProvider"/>
    /// </summary>
    public partial class ThetaDataProvider : SynchronizingHistoryProvider
    {
        /// <summary>
        /// Indicates whether the warning for invalid <see cref="SecurityType"/> has been fired.
        /// </summary>
        private volatile bool _invalidSecurityTypeWarningFired;

        /// <summary>
        /// Indicates whether a warning for an invalid start time has been fired, where the start time is greater than or equal to the end time in UTC.
        /// </summary>
        private volatile bool _invalidStartTimeWarningFired;

        /// <summary>
        /// Indicates whether an warning should be raised when encountering invalid open interest data for an option security type at daily resolution.
        /// </summary>
        /// <remarks>
        /// This flag is set to true when an error is detected for invalid open interest data for options at daily resolution.
        /// </remarks>
        private volatile bool _invalidOpenInterestWarningFired;

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
            if (!CanSubscribe(historyRequest.Symbol))
            {
                if (!_invalidSecurityTypeWarningFired)
                {
                    _invalidSecurityTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Unsupported SecurityType '{historyRequest.Symbol.SecurityType}' for symbol '{historyRequest.Symbol}'");
                }
                return null;
            }

            if (historyRequest.StartTimeUtc >= historyRequest.EndTimeUtc)
            {
                if (!_invalidStartTimeWarningFired)
                {
                    _invalidStartTimeWarningFired = true;
                    Log.Error($"{nameof(ThetaDataProvider)}.{nameof(GetHistory)}: Error - The start date in the history request must come before the end date. No historical data will be returned.");
                }
                return null;
            }

            if (historyRequest.TickType != TickType.OpenInterest && historyRequest.Resolution != Resolution.Daily && historyRequest.Symbol.SecurityType != SecurityType.Option)
            {
                if (!_invalidOpenInterestWarningFired)
                {
                    _invalidOpenInterestWarningFired = true;
                    Log.Trace($"Invalid data request: TickType 'OpenInterest' only supports Resolution 'Daily' and SecurityType 'Option'. Requested: Resolution '{historyRequest.Resolution}', SecurityType '{historyRequest.Symbol.SecurityType}'.");
                }
                return null;
            }

            var restRequest = new RestRequest(Method.GET);

            var startDate = GetTickTime(historyRequest.Symbol, historyRequest.StartTimeUtc).ConvertToThetaDataDateFormat();
            var endDate = GetTickTime(historyRequest.Symbol, historyRequest.EndTimeUtc).ConvertToThetaDataDateFormat();

            restRequest.AddQueryParameter("start_date", startDate);
            restRequest.AddQueryParameter("end_date", endDate);

            switch (historyRequest.Symbol.SecurityType)
            {
                case SecurityType.Option:
                    return GetOptionHistoryData(restRequest, historyRequest.Symbol, historyRequest.Resolution, historyRequest.TickType);
            }

            return null;
        }

        public IEnumerable<BaseData>? GetOptionHistoryData(RestRequest optionRequest, Symbol symbol, Resolution resolution, TickType tickType)
        {
            var ticker = _symbolMapper.GetBrokerageSymbol(symbol).Split(',');

            optionRequest.AddQueryParameter("root", ticker[0]);
            optionRequest.AddQueryParameter("exp", ticker[1]);
            optionRequest.AddQueryParameter("strike", ticker[2]);
            optionRequest.AddQueryParameter("right", ticker[3]);

            if (resolution == Resolution.Daily)
            {
                switch (tickType)
                {
                    case TickType.Trade:
                        optionRequest.Resource = "/hist/option/eod";
                        return GetOptionEndOfDay(optionRequest, symbol, resolution.ToTimeSpan());
                    case TickType.OpenInterest:
                        optionRequest.Resource = "/hist/option/open_interest";
                        return GetHistoricalOpenInterestData(optionRequest, symbol);
                    default:
                        throw new ArgumentException("");
                }
            }
            else
            {
                switch (tickType)
                {
                    case TickType.Trade:
                        optionRequest.Resource = "/hist/option/trade";
                        return GetHistoricalTradeData(optionRequest, symbol);
                    case TickType.Quote:
                        optionRequest.AddQueryParameter("ivl", GetIntervalsInMilliseconds(resolution));
                        optionRequest.Resource = "/hist/option/quote";
                        return GetHistoricalQuoteData(optionRequest, symbol);
                    default:
                        throw new ArgumentException("");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private IEnumerable<BaseData> GetHistoricalOpenInterestData(RestRequest request, Symbol symbol)
        {
            foreach (var openInterest in _restApiClient.ExecuteRequest<BaseResponse<OpenInterestResponse>>(request).Response)
            {
                // ThetaData API: Eastern Time (ET) time zone.
                var openInterestDateTime = openInterest.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(openInterest.TimeMilliseconds);
                yield return new OpenInterest(openInterestDateTime, symbol, openInterest.OpenInterest);
            }
        }

        private IEnumerable<BaseData> GetHistoricalTradeData(RestRequest request, Symbol symbol)
        {
            foreach (var trade in _restApiClient.ExecuteRequest<BaseResponse<TradeResponse>>(request).Response)
            {
                // ThetaData API: Eastern Time (ET) time zone.
                var tradeDateTime = trade.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(trade.TimeMilliseconds);
                yield return new Tick(tradeDateTime, symbol, trade.Condition.ToStringInvariant(), string.Empty, trade.Size, trade.Price);
            }
        }

        private IEnumerable<BaseData> GetHistoricalQuoteData(RestRequest request, Symbol symbol)
        {
            foreach (var quote in _restApiClient.ExecuteRequest<BaseResponse<QuoteResponse>>(request).Response)
            {
                // ThetaData API: Eastern Time (ET) time zone.
                var quoteDateTime = quote.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(quote.TimeMilliseconds);
                yield return new Tick(quoteDateTime, symbol, ThetaDataExtensions.QuoteConditions[quote.AskCondition], string.Empty, quote.BidSize, quote.BidPrice, quote.AskSize, quote.AskPrice);
            }
        }

        private IEnumerable<BaseData>? GetOptionEndOfDay(RestRequest request, Symbol symbol, TimeSpan period)
        {
            foreach (var endOfDay in _restApiClient.ExecuteRequest<BaseResponse<EndOfDayReportResponse>>(request).Response)
            {
                var tradeDateTime = GetTickTime(symbol, endOfDay.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(endOfDay.LastTradeTimeMilliseconds));

                // Interlocked.Increment(ref _dataPointCount);

                yield return new TradeBar(tradeDateTime, symbol, endOfDay.Open, endOfDay.High, endOfDay.Low, endOfDay.Close, endOfDay.Volume, period);
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
            _ => throw new NotSupportedException($"The resolution type '{resolution}' is not supported.")
        };
    }
}