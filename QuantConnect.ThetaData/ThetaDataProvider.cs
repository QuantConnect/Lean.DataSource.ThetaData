﻿/*
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
using System.Globalization;
using QuantConnect.Securities;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Configuration;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;
using QuantConnect.Lean.DataSource.ThetaData.Models.WebSocket;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// 
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
        /// Manages and provides access to exchange trading hours information.
        /// </summary>
        private readonly MarketHoursDatabase _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();

        /// <summary>
        /// A dictionary that maps symbols to their respective time zones based on the exchange they belong to.
        /// </summary>
        private readonly Dictionary<Symbol, DateTimeZone> _symbolExchangeTimeZones = new();

        /// <summary>
        /// Helper class is doing to subscribe / unsubscribe process.
        /// </summary>
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        /// <summary>
        /// Ensures thread-safe synchronization when updating aggregation tick data, such as quotes or trades.
        /// </summary>
        private object _lock = new object();

        public bool IsConnected => throw new NotImplementedException();

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

            _symbolMapper = new ThetaDataSymbolMapper();

            ThetaDataWebSocketClientWrapper webSocketClient = new(_symbolMapper, OnMessage);
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += (symbols, _) => webSocketClient.Subscribe(symbols);
            _subscriptionManager.UnsubscribeImpl += (symbols, _) => webSocketClient.Unsubscribe(symbols);
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
                leanSymbol = _symbolMapper.GetLeanSymbolByBrokerageContract(
                    json.Contract.Value.Root,
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
            var timeDateQuote = GetTickTime(symbol, DateTime.ParseExact(webSocketQuote.Date, "yyyyMMdd", CultureInfo.InvariantCulture).AddMilliseconds(webSocketQuote.DayTimeMilliseconds));
            // TODO: Exchange name.
            var tick = new Tick(timeDateQuote, symbol, webSocketQuote.BidCondition.ToStringInvariant(), string.Empty,
            bidSize: webSocketQuote.BidSize, bidPrice: webSocketQuote.BidPrice,
            askSize: webSocketQuote.AskSize, askPrice: webSocketQuote.AskPrice);

            lock (_lock)
            {
                _dataAggregator.Update(tick);
            }
        }

        private void HandleTradeMessage(Symbol symbol, WebSocketTrade webSocketTrade)
        {
            var timeDateTrade = GetTickTime(symbol, DateTime.ParseExact(webSocketTrade.Date, "yyyyMMdd", CultureInfo.InvariantCulture).AddMilliseconds(webSocketTrade.DayTimeMilliseconds));
            // TODO: Exchange name.
            var tick = new Tick(timeDateTrade, symbol, webSocketTrade.Condition.ToStringInvariant(), string.Empty, webSocketTrade.Size, webSocketTrade.Price);
            lock (_lock)
            {
                _dataAggregator.Update(tick);
            }
        }

        /// <summary>
        /// Converts the given UTC time into the symbol security exchange time zone
        /// </summary>
        private DateTime GetTickTime(Symbol symbol, DateTime utcTime)
        {
            if (!_symbolExchangeTimeZones.TryGetValue(symbol, out var exchangeTimeZone))
            {
                // read the exchange time zone from market-hours-database
                if (_marketHoursDatabase.TryGetEntry(symbol.ID.Market, symbol, symbol.SecurityType, out var entry))
                {
                    exchangeTimeZone = entry.ExchangeHours.TimeZone;
                }
                // If there is no entry for the given Symbol, default to New York
                else
                {
                    exchangeTimeZone = TimeZones.NewYork;
                }

                _symbolExchangeTimeZones.Add(symbol, exchangeTimeZone);
            }

            return utcTime.ConvertFromUtc(exchangeTimeZone);
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
                symbol.SecurityType == SecurityType.Option;
        }
    }
}
