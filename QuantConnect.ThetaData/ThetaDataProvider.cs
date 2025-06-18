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

using RestSharp;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Api;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Logging;
using Newtonsoft.Json.Linq;
using QuantConnect.Interfaces;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;
using QuantConnect.Lean.DataSource.ThetaData.Models.WebSocket;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;
using QuantConnect.Lean.DataSource.ThetaData.Models.SubscriptionPlans;

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
        private IDataAggregator _dataAggregator;

        /// <summary>
        /// Provides the TheData mapping between Lean symbols and brokerage specific symbols.
        /// </summary>
        private ThetaDataSymbolMapper _symbolMapper;

        /// <summary>
        /// Helper class is doing to subscribe / unsubscribe process.
        /// </summary>
        private EventBasedDataQueueHandlerSubscriptionManager _subscriptionManager;

        /// <summary>
        /// Represents an instance of a WebSocket client wrapper for ThetaData.net.
        /// </summary>
        private ThetaDataWebSocketClientWrapper _webSocketClient;

        /// <summary>
        /// Represents a client for interacting with the Theta Data REST API by sending HTTP requests.
        /// </summary>
        private ThetaDataRestApiClient _restApiClient;

        /// <summary>
        /// Ensures thread-safe synchronization when updating aggregation tick data, such as quotes or trades.
        /// </summary>
        private object _lock = new object();

        /// <summary>
        /// Represents the subscription plan assigned to the user.
        /// </summary>
        private ISubscriptionPlan _userSubscriptionPlan;

        /// <summary>
        /// Indicates whether the user's subscription plan allows access to real-time updates on quote and trade channels.
        /// </summary>
        private bool _streamingAvailable = false;

        /// <summary>
        /// Indicates whether the initialization process has been completed successfully.
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// Represents the current state of internet connectivity.
        /// </summary>
        /// <remarks>
        /// This boolean flag is used to track whether the internet connection is currently disconnected.
        /// </remarks>
        private volatile bool isInternetDisconnected;

        /// <summary>
        /// A thread-safe dictionary that stores the best Bid and Offer by brokerage symbols.
        /// </summary>
        private readonly ConcurrentDictionary<Symbol, LevelOneService> _levelOneServiceBySymbol = new();

        /// <summary>
        /// The time provider instance. Used for improved testability
        /// </summary>
        protected virtual ITimeProvider TimeProvider { get; } = RealTimeProvider.Instance;

        /// <inheritdoc />
        public bool IsConnected => _webSocketClient.IsOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataProvider"/>
        /// </summary>
        public ThetaDataProvider() : this(Config.Get("thetadata-subscription-plan"))
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataProvider"/> class with the specified price plan.
        /// </summary>
        /// <param name="pricePlan">The price plan for the Theta Data Provider. This parameter is required to initialize the provider.</param>
        /// <remarks>
        /// If the <paramref name="pricePlan"/> is null, empty, or consists only of white-space characters, 
        /// the initialization will be aborted, as a valid price plan is necessary for the provider's functionality.
        /// </remarks>
        public ThetaDataProvider(string pricePlan)
        {
            if (string.IsNullOrWhiteSpace(pricePlan))
            {
                // If the Price Plan is not provided, we can't do anything.
                // The handler might going to be initialized using a node packet job.
                return;
            }

            Initialize(pricePlan);
        }

        /// <summary>
        /// Event handler for WebSocket errors in the ThetaDataWebSocketClientWrapper.
        /// </summary>
        /// <param name="_">The sender of the event.</param>
        /// <param name="e">The WebSocketError object containing information about the error.</param>
        /// <remarks>
        /// This method throws an Exception with a message containing information about the error.
        /// </remarks>
        private void OnError(object? _, Brokerages.WebSocketError e)
        {
            throw new Exception($"{nameof(ThetaDataProvider)}.{nameof(OnError)}: {e.Message}");
        }

        /// <summary>
        /// Disposes of the resources used by the WebSocketClientWrapper instance.
        /// Ensures that the WebSocket connection is properly closed and other resources are released safely.
        /// </summary>
        public void Dispose()
        {
            _webSocketClient?.CloseWebSocketConnection();
            _dataAggregator?.DisposeSafely();
        }

        /// <inheritdoc />
        public IEnumerator<BaseData>? Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol) || !_streamingAvailable)
            {
                return null;
            }

            if (!_levelOneServiceBySymbol.TryGetValue(dataConfig.Symbol, out var levelOneService))
            {
                _levelOneServiceBySymbol[dataConfig.Symbol] = new(dataConfig.Symbol, _dataAggregator);
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

            _levelOneServiceBySymbol.TryRemove(dataConfig.Symbol, out _);
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
            if (_initialized)
            {
                return;
            }

            if (!job.BrokerageData.TryGetValue("thetadata-subscription-plan", out var pricePlan))
            {
                throw new ArgumentException($"{nameof(ThetaDataProvider)}.{nameof(SetJob)}: The ThetaData subscription plan is missed from the BrokerageData Live data.");
            }

            Initialize(pricePlan);
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
                case WebSocketHeaderType.Status when json.Header.Status == "DISCONNECTED":
                    isInternetDisconnected = true;
                    break;
                case WebSocketHeaderType.Status when isInternetDisconnected:
                    isInternetDisconnected = !_webSocketClient.Subscribe(_subscriptionManager.GetSubscribedSymbols(), true);
                    break;
                case WebSocketHeaderType.Status when json.Header.Status == "CONNECTED":
                    break;
                case WebSocketHeaderType.Ohlc:
                    break;
                default:
                    Log.Debug(message);
                    break;
            }
        }

        private void HandleQuoteMessage(Symbol symbol, WebSocketQuote quote)
        {
            if (_levelOneServiceBySymbol.TryGetValue(symbol, out var levelOneService))
            {
                if (levelOneService.BestAskPrice == quote.AskPrice && levelOneService.BestAskSize == quote.AskSize
                    && levelOneService.BestBidPrice == quote.BidPrice && levelOneService.BestBidSize == quote.BidSize)
                {
                    return;
                }

                levelOneService.UpdateQuote(DateTime.UtcNow, quote.BidPrice, quote.BidSize, quote.AskPrice, quote.AskSize);
            }
            else
            {
                Log.Error($"{nameof(ThetaDataProvider)}.{nameof(HandleQuoteMessage)}: Symbol {symbol} not found in {nameof(_levelOneServiceBySymbol)}. This could indicate an unexpected symbol or a missing initialization step.");
            }
        }

        private void HandleTradeMessage(Symbol symbol, WebSocketTrade trade)
        {
            if (!_levelOneServiceBySymbol.TryGetValue(symbol, out var levelOneService))
            {
                Log.Error($"{nameof(ThetaDataProvider)}.{nameof(HandleTradeMessage)}: Symbol {symbol} not found in {nameof(_levelOneServiceBySymbol)}. This could indicate an unexpected symbol or a missing initialization step.");
                return;
            }

            levelOneService.UpdateLastTrade(DateTime.UtcNow, trade.Size, trade.Price, trade.Condition.ToStringInvariant(), trade.Exchange.TryGetExchangeOrDefault());
        }

        /// <summary>
        /// Checks if this brokerage supports the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <returns>returns true if brokerage supports the specified symbol; otherwise false</returns>
        private bool CanSubscribe(Symbol symbol)
        {
            return
                symbol.Value.IndexOfInvariant("universe", true) == -1 &&
                !symbol.IsCanonical() &&
                _symbolMapper.SupportedSecurityType.Contains(symbol.SecurityType);
        }

        /// <summary>
        /// Retrieves the subscription plan associated with the current user based on the provided price plan.
        /// </summary>
        /// <param name="pricePlan">The price plan for the user's subscription. If not provided, defaults to 'Free'.</param>
        /// <returns>
        /// An instance of the <see cref="ISubscriptionPlan"/> interface representing the subscription plan of the user.
        /// </returns>
        private ISubscriptionPlan GetUserSubscriptionPlan(string pricePlan)
        {
            if (string.IsNullOrEmpty(pricePlan))
            {
                Log.Trace($"{nameof(ThetaDataProvider)}.{nameof(GetUserSubscriptionPlan)}: No price plan provided. Defaulting to 'Free' plan.");
                pricePlan = "Free";
            }

            if (!Enum.TryParse<SubscriptionPlanType>(pricePlan, out var parsedPricePlan) || !Enum.IsDefined(typeof(SubscriptionPlanType), parsedPricePlan))
            {
                throw new ArgumentException($"An error occurred while parsing the price plan '{pricePlan}'. Please ensure that the provided price plan is valid and supported by the system.");
            }

            ISubscriptionPlan userSubscriptionPlan = parsedPricePlan switch
            {
                SubscriptionPlanType.Free => new FreeSubscriptionPlan(),
                SubscriptionPlanType.Value => new ValueSubscriptionPlan(),
                SubscriptionPlanType.Standard => new StandardSubscriptionPlan(),
                SubscriptionPlanType.Pro => new ProSubscriptionPlan(),
                _ => throw new ArgumentException($"{nameof(ThetaDataProvider)}.{nameof(GetUserSubscriptionPlan)}: Invalid subscription plan type.")
            };

            if (userSubscriptionPlan.MaxStreamingContracts > 0)
            {
                _streamingAvailable = true;
            }
            else
            {
                Log.Error($"{nameof(ThetaDataProvider)}.{nameof(GetUserSubscriptionPlan)}: Insufficient permissions to access or modify subscription plan for the user. Streaming service is not available for this user.");
            }

            return userSubscriptionPlan;
        }

        /// <summary>
        /// Initializes the necessary components for data aggregation, subscription management, 
        /// API clients, and WebSocket client setup.
        /// </summary>
        /// <remarks>
        /// This method sets up the data aggregator, user subscription plan, REST API client, 
        /// symbol mapper, option chain provider, and WebSocket client wrapper. It also configures 
        /// the subscription manager to handle subscription and unsubscription requests.
        /// </remarks>
        private void Initialize(string pricePlan)
        {
            _dataAggregator = Composer.Instance.GetPart<IDataAggregator>();
            if (_dataAggregator == null)
            {
                _dataAggregator =
                    Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"), forceTypeNameOnExisting: false);
            }

            _userSubscriptionPlan = GetUserSubscriptionPlan(pricePlan);
            _initialized = true;

            _restApiClient = new ThetaDataRestApiClient(_userSubscriptionPlan.RateGate!);
            _symbolMapper = new ThetaDataSymbolMapper();

            _optionChainProvider = new ThetaDataOptionChainProvider(_symbolMapper, _restApiClient);

            _webSocketClient = new ThetaDataWebSocketClientWrapper(_symbolMapper, _userSubscriptionPlan.MaxStreamingContracts, OnMessage, OnError);
            _subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            _subscriptionManager.SubscribeImpl += (symbols, _) => _webSocketClient.Subscribe(symbols);
            _subscriptionManager.UnsubscribeImpl += (symbols, _) => _webSocketClient.Unsubscribe(symbols);

            ValidateSubscription();
        }

        private class ModulesReadLicenseRead : Api.RestResponse
        {
            [JsonProperty(PropertyName = "license")]
            public string License;

            [JsonProperty(PropertyName = "organizationId")]
            public string OrganizationId;
        }

        /// <summary>
        /// Validate the user of this project has permission to be using it via our web API.
        /// </summary>
        private static void ValidateSubscription()
        {
            try
            {
                const int productId = 344;
                var userId = Globals.UserId;
                var token = Globals.UserToken;
                var organizationId = Globals.OrganizationID;
                // Verify we can authenticate with this user and token
                var api = new ApiConnection(userId, token);
                if (!api.Connected)
                {
                    throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
                }
                // Compile the information we want to send when validating
                var information = new Dictionary<string, object>()
                {
                    {"productId", productId},
                    {"machineName", Environment.MachineName},
                    {"userName", Environment.UserName},
                    {"domainName", Environment.UserDomainName},
                    {"os", Environment.OSVersion}
                };
                // IP and Mac Address Information
                try
                {
                    var interfaceDictionary = new List<Dictionary<string, object>>();
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up))
                    {
                        var interfaceInformation = new Dictionary<string, object>();
                        // Get UnicastAddresses
                        var addresses = nic.GetIPProperties().UnicastAddresses
                            .Select(uniAddress => uniAddress.Address)
                            .Where(address => !IPAddress.IsLoopback(address)).Select(x => x.ToString());
                        // If this interface has non-loopback addresses, we will include it
                        if (!addresses.IsNullOrEmpty())
                        {
                            interfaceInformation.Add("unicastAddresses", addresses);
                            // Get MAC address
                            interfaceInformation.Add("MAC", nic.GetPhysicalAddress().ToString());
                            // Add Interface name
                            interfaceInformation.Add("name", nic.Name);
                            // Add these to our dictionary
                            interfaceDictionary.Add(interfaceInformation);
                        }
                    }
                    information.Add("networkInterfaces", interfaceDictionary);
                }
                catch (Exception)
                {
                    // NOP, not necessary to crash if fails to extract and add this information
                }
                // Include our OrganizationId if specified
                if (!string.IsNullOrEmpty(organizationId))
                {
                    information.Add("organizationId", organizationId);
                }
                var request = new RestRequest("modules/license/read", Method.POST) { RequestFormat = DataFormat.Json };
                request.AddParameter("application/json", JsonConvert.SerializeObject(information), ParameterType.RequestBody);
                api.TryRequest(request, out ModulesReadLicenseRead result);
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Request for subscriptions from web failed, Response Errors : {string.Join(',', result.Errors)}");
                }

                var encryptedData = result.License;
                // Decrypt the data we received
                DateTime? expirationDate = null;
                long? stamp = null;
                bool? isValid = null;
                if (encryptedData != null)
                {
                    // Fetch the org id from the response if it was not set, we need it to generate our validation key
                    if (string.IsNullOrEmpty(organizationId))
                    {
                        organizationId = result.OrganizationId;
                    }
                    // Create our combination key
                    var password = $"{token}-{organizationId}";
                    var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    // Split the data
                    var info = encryptedData.Split("::");
                    var buffer = Convert.FromBase64String(info[0]);
                    var iv = Convert.FromBase64String(info[1]);
                    // Decrypt our information
                    using var aes = new AesManaged();
                    var decryptor = aes.CreateDecryptor(key, iv);
                    using var memoryStream = new MemoryStream(buffer);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var streamReader = new StreamReader(cryptoStream);
                    var decryptedData = streamReader.ReadToEnd();
                    if (!decryptedData.IsNullOrEmpty())
                    {
                        var jsonInfo = JsonConvert.DeserializeObject<JObject>(decryptedData);
                        expirationDate = jsonInfo["expiration"]?.Value<DateTime>();
                        isValid = jsonInfo["isValid"]?.Value<bool>();
                        stamp = jsonInfo["stamped"]?.Value<int>();
                    }
                }
                // Validate our conditions
                if (!expirationDate.HasValue || !isValid.HasValue || !stamp.HasValue)
                {
                    throw new InvalidOperationException("Failed to validate subscription.");
                }

                var nowUtc = DateTime.UtcNow;
                var timeSpan = nowUtc - Time.UnixTimeStampToDateTime(stamp.Value);
                if (timeSpan > TimeSpan.FromHours(12))
                {
                    throw new InvalidOperationException("Invalid API response.");
                }
                if (!isValid.Value)
                {
                    throw new ArgumentException($"Your subscription is not valid, please check your product subscriptions on our website.");
                }
                if (expirationDate < nowUtc)
                {
                    throw new ArgumentException($"Your subscription expired {expirationDate}, please renew in order to use this product.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"{nameof(ThetaDataProvider)}.{nameof(ValidateSubscription)}: Failed during validation, shutting down. Error : {e.Message}");
                throw;
            }
        }
    }
}
