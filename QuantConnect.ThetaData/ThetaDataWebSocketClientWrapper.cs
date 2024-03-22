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
using QuantConnect.Logging;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// WebSocket client wrapper for ThetaData.net
    /// </summary>
    public class ThetaDataWebSocketClientWrapper : WebSocketClientWrapper
    {
        /// <summary>
        /// Represents the base URL endpoint for receiving stream messages from Theta Data.
        /// A single connection to this endpoint is required for both sending streaming requests and receiving messages.
        /// </summary>
        private static readonly string BaseUrl = Config.Get("thetadata-ws-url", "ws://127.0.0.1:25520/v1/events");

        /// <summary>
        /// Represents the array of required subscription channels for receiving real-time market data.
        /// Subscribing to these channels allows access to specific types of data streams from the Options Price Reporting Authority (OPRA) feed.
        /// </summary>
        /// <remarks>
        /// Available Channels:
        ///     - TRADE: This channel provides every trade executed for a specified contract reported on the OPRA feed.
        ///     - QUOTE: This channel provides every National Best Bid and Offer (NBBO) quote for US Options reported on the OPRA feed for the specified contract.
        /// </remarks>
        private static readonly string[] Channels = { "TRADE", "QUOTE" };

        /// <summary>
        /// Represents a method that handles messages received from a WebSocket.
        /// </summary>
        private readonly Action<string> _messageHandler;

        /// <summary>
        /// Represents a way of tracking streaming requests made.
        /// The field should be increased for each new stream request made. 
        /// </summary>
        private int _idRequestCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataWebSocketClientWrapper"/>
        /// </summary>
        /// <param name="messageHandler">The method that handles messages received from the WebSocket client.</param>
        public ThetaDataWebSocketClientWrapper(Action<string> messageHandler)
        {
            Initialize(BaseUrl);

            _messageHandler = messageHandler;

            Closed += OnClosed;
            Message += OnMessage;
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        public bool Subscribe(IEnumerable<Symbol> symbols)
        {
            if (!IsOpen)
            {
                Connect();
            }

            foreach (var symbol in symbols)
            {
                foreach (var jsonMessage in GetContractSubscriptionMessage(true, symbol))
                {
                    Send(jsonMessage);
                    Interlocked.Increment(ref _idRequestCount);
                }
            }

            return true;
        }

        private IEnumerable<string> GetContractSubscriptionMessage(bool isSubscribe, Symbol symbol)
        {
            var ticker = symbol.ID.Symbol;
            var expirationDate = symbol.ID.Date.ToString("yyyyMMdd");
            var strikePrice = Math.Truncate(symbol.ID.StrikePrice * 1000m).ToStringInvariant();
            var optionRight = symbol.ID.OptionRight == OptionRight.Call ? "C" : "P";
            foreach (var channel in Channels)
            {
                yield return GetMessage(isSubscribe, channel, ticker, expirationDate, strikePrice, optionRight);
            }
        }

        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        public bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                foreach (var jsonMessage in GetContractSubscriptionMessage(false, symbol))
                {
                    Send(jsonMessage);
                    Interlocked.Increment(ref _idRequestCount);
                }
            }
            return true;
        }

        /// <summary>
        /// Constructs a message for subscribing or unsubscribing to a financial instrument on a specified channel.
        /// </summary>
        /// <param name="isSubscribe">A boolean value indicating whether to subscribe (true) or unsubscribe (false).</param>
        /// <param name="channelName">The name of the channel to subscribe or unsubscribe from. <see cref="Channels"/></param>
        /// <param name="ticker">The ticker symbol of the financial instrument.</param>
        /// <param name="expirationDate">The expiration date of the option contract.</param>
        /// <param name="strikePrice">The strike price of the option contract.</param>
        /// <param name="optionRight">The option type, either "C" for call or "P" for put.</param>
        /// <returns>A json string representing the constructed message.</returns>
        private string GetMessage(bool isSubscribe, string channelName, string ticker, string expirationDate, string strikePrice, string optionRight)
        {
            return JsonConvert.SerializeObject(new
            {
                msg_type = "STREAM",
                sec_type = "OPTION",
                req_type = channelName,
                add = isSubscribe,
                id = _idRequestCount,
                contract = new
                {
                    root = ticker,
                    expiration = expirationDate,
                    strike = strikePrice,
                    right = optionRight
                }
            });
        }


        /// <summary>
        /// Event handler for processing WebSocket messages.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="webSocketMessage">The WebSocket message received.</param>
        private void OnMessage(object? sender, WebSocketMessage webSocketMessage)
        {
            var e = (TextMessage)webSocketMessage.Data;

            _messageHandler?.Invoke(e.Message);
        }

        /// <summary>
        /// Event handler for processing WebSocket close data.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="webSocketCloseData">The WebSocket Close Data received.</param>
        private void OnClosed(object? sender, WebSocketCloseData webSocketCloseData)
        {
            Log.Trace($"{nameof(ThetaDataWebSocketClientWrapper)}.{nameof(OnClosed)}: {webSocketCloseData.Reason}");
        }
    }
}
