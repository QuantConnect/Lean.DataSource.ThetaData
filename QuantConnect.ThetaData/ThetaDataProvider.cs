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


using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
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
        /// Helper class is doing to subscribe / unsubscribe process.
        /// </summary>
        private readonly EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        public bool IsConnected => throw new NotImplementedException();

        public ThetaDataProvider()
        {
            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>();
            if (_dataAggregator == null)
            {
                _dataAggregator =
                    Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"), forceTypeNameOnExisting: false);
            }

            ThetaDataWebSocketClientWrapper webSocketClient = new(OnMessage);
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += (symbols, _) => webSocketClient.Subscribe(symbols);
            _subscriptionManager.UnsubscribeImpl += (symbols, _) => webSocketClient.Unsubscribe(symbols);
        }

        public void Dispose()
        {
            _dataAggregator?.DisposeSafely();
        }

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

            switch (json?.Header.Type)
            {
                case WebSocketHeaderType.Quote:
                    break;
                case WebSocketHeaderType.Trade:
                    break;
                case WebSocketHeaderType.Status:
                    break;
                default:
                    Log.Debug(message);
                    break;
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
                symbol.SecurityType == SecurityType.Option;
        }
    }
}
