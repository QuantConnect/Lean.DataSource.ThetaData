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
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;
using QuantConnect.Lean.DataSource.ThetaData.Models.WebSocket;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IDataQueueHandler"/>
    /// </summary>
    public partial class ThetaDataProvider : IDataQueueHandler
    {
        /// <summary>
        /// Aggregates ticks and bars based on given subscriptions.
        /// </summary>
        private readonly IDataAggregator _dataAggregator;

        /// <summary>
        /// Provides the TheData mapping between Lean symbols and brokerage specific symbols.
        /// </summary>
        private readonly ThetaDataSymbolMapper _symbolMapper;

        /// <summary>
        /// Helper class is doing to subscribe / unsubscribe process.
        /// </summary>
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        /// <summary>
        /// Represents an instance of a WebSocket client wrapper for ThetaData.net.
        /// </summary>
        private readonly ThetaDataWebSocketClientWrapper _webSocketClient;

        /// <summary>
        /// Represents a client for interacting with the Theta Data REST API by sending HTTP requests.
        /// </summary>
        private readonly ThetaDataRestApiClient _restApiClient;

        /// <summary>
        /// Ensures thread-safe synchronization when updating aggregation tick data, such as quotes or trades.
        /// </summary>
        private object _lock = new object();

        /// <summary>
        /// The time provider instance. Used for improved testability
        /// </summary>
        protected virtual ITimeProvider TimeProvider { get; } = RealTimeProvider.Instance;

        /// <inheritdoc />
        public bool IsConnected => _webSocketClient.IsOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataProvider"/>
        /// </summary>
        public ThetaDataProvider()
        {
            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>();
            if (_dataAggregator == null)
            {
                _dataAggregator =
                    Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"), forceTypeNameOnExisting: false);
            }

            _restApiClient = new ThetaDataRestApiClient();
            _symbolMapper = new ThetaDataSymbolMapper();

            _optionChainProvider = new CachingOptionChainProvider(new ThetaDataOptionChainProvider(_symbolMapper, _restApiClient));

            _webSocketClient = new ThetaDataWebSocketClientWrapper(_symbolMapper, OnMessage);
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += (symbols, _) => _webSocketClient.Subscribe(symbols);
            _subscriptionManager.UnsubscribeImpl += (symbols, _) => _webSocketClient.Unsubscribe(symbols);
        }

        public void Dispose()
        {
            _dataAggregator?.DisposeSafely();
        }

        /// <inheritdoc />
        public IEnumerator<BaseData>? Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return null;
            }

            var enumerator = _dataAggregator.Add(dataConfig, newDataAvailableHandler);
            _subscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <inheritdoc />
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            _subscriptionManager.Unsubscribe(dataConfig);
            _dataAggregator.Remove(dataConfig);
        }

        /// <inheritdoc />
        public void SetJob(LiveNodePacket job)
        {
        }

        private void OnMessage(string message)
        {
            var json = JsonConvert.DeserializeObject<WebSocketResponse>(message);

            var leanSymbol = default(Symbol);
            if (json != null && json.Header.Type != WebSocketHeaderType.Status && json.Contract.HasValue)
            {
                leanSymbol = _symbolMapper.GetLeanSymbol(
                    json.Contract.Value.Root,
                    json.Contract.Value.SecurityType,
                    json.Contract.Value.Expiration,
                    json.Contract.Value.Strike,
                    json.Contract.Value.Right);
            }

            switch (json?.Header.Type)
            {
                case WebSocketHeaderType.Quote when leanSymbol != null && json.Quote != null:
                    HandleQuoteMessage(leanSymbol, json.Quote.Value);
                    break;
                case WebSocketHeaderType.Trade when leanSymbol != null && json.Trade != null:
                    HandleTradeMessage(leanSymbol, json.Trade.Value);
                    break;
                case WebSocketHeaderType.Status:
                    break;
                default:
                    Log.Debug(message);
                    break;
            }
        }

        private void HandleQuoteMessage(Symbol symbol, WebSocketQuote webSocketQuote)
        {
            // ThetaData API: Eastern Time (ET) time zone.
            var timeDateQuote = webSocketQuote.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(webSocketQuote.DayTimeMilliseconds);
            var tick = new Tick(timeDateQuote, symbol, webSocketQuote.BidCondition.ToStringInvariant(), ThetaDataExtensions.Exchanges[webSocketQuote.BidExchange],
            bidSize: webSocketQuote.BidSize, bidPrice: webSocketQuote.BidPrice,
            askSize: webSocketQuote.AskSize, askPrice: webSocketQuote.AskPrice);

            lock (_lock)
            {
                _dataAggregator.Update(tick);
            }
        }

        private void HandleTradeMessage(Symbol symbol, WebSocketTrade webSocketTrade)
        {
            // ThetaData API: Eastern Time (ET) time zone.
            var timeDateTrade = webSocketTrade.Date.ConvertFromThetaDataDateFormat().AddMilliseconds(webSocketTrade.DayTimeMilliseconds);
            var tick = new Tick(timeDateTrade, symbol, webSocketTrade.Condition.ToStringInvariant(), ThetaDataExtensions.Exchanges[webSocketTrade.Exchange], webSocketTrade.Size, webSocketTrade.Price);
            lock (_lock)
            {
                _dataAggregator.Update(tick);
            }
        }

        /// <summary>
        /// Checks if this brokerage supports the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <returns>returns true if brokerage supports the specified symbol; otherwise false</returns>
        private static bool CanSubscribe(Symbol symbol)
        {
            return
                symbol.Value.IndexOfInvariant("universe", true) == -1 &&
                !symbol.IsCanonical() &&
                symbol.SecurityType == SecurityType.Option || symbol.SecurityType == SecurityType.IndexOption;
        }
    }
}
