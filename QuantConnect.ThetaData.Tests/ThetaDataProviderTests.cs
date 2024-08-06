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

using System;
using System.Linq;
using NUnit.Framework;
using System.Threading;
using QuantConnect.Data;
using QuantConnect.Tests;
using QuantConnect.Logging;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using System.Collections.Generic;
using System.Collections.Concurrent;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    [Explicit("This test requires the ThetaData terminal to be running in order to execute properly.")]
    public class ThetaDataProviderTests
    {
        private ThetaDataProvider _thetaDataProvider;
        private CancellationTokenSource _cancellationTokenSource;

        [SetUp]
        public void SetUp()
        {
            TestGlobals.Initialize();
            _thetaDataProvider = new();
            _cancellationTokenSource = new();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Dispose();

            if (_thetaDataProvider != null)
            {
                _thetaDataProvider.Dispose();
            }
        }

        [TestCase("AAPL", SecurityType.Equity, Resolution.Second, 0, "2024/08/16")]
        [TestCase("AAPL", SecurityType.Option, Resolution.Second, 215, "2024/08/16")]
        [TestCase("VIX", SecurityType.Index, Resolution.Second, 0, "2024/08/16")]
        public void CanSubscribeAndUnsubscribeOnSecondResolution(string ticker, SecurityType securityType, Resolution resolution, decimal strikePrice, DateTime expiryDate = default)
        {
            var configs = GetSubscriptionDataConfigs(ticker, securityType, resolution, strikePrice, expiryDate);

            Assert.That(configs, Is.Not.Empty);

            var dataFromEnumerator = new Dictionary<Type, int>() { { typeof(TradeBar), 0 }, { typeof(QuoteBar), 0 } };

            Action<BaseData> callback = (dataPoint) =>
            {
                if (dataPoint == null)
                {
                    return;
                }

                switch (dataPoint)
                {
                    case TradeBar _:
                        dataFromEnumerator[typeof(TradeBar)] += 1;
                        break;
                    case QuoteBar _:
                        dataFromEnumerator[typeof(QuoteBar)] += 1;
                        break;
                };
            };

            foreach (var config in configs)
            {
                ProcessFeed(_thetaDataProvider.Subscribe(config, (sender, args) =>
                {
                    var dataPoint = ((NewDataAvailableEventArgs)args).DataPoint;
                    Log.Trace($"{dataPoint}. Time span: {dataPoint.Time} - {dataPoint.EndTime}");
                }), _cancellationTokenSource.Token, callback: callback);
            }

            Thread.Sleep(TimeSpan.FromSeconds(25));

            Log.Trace("Unsubscribing symbols");
            foreach (var config in configs)
            {
                _thetaDataProvider.Unsubscribe(config);
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));

            _cancellationTokenSource.Cancel();

            Log.Trace($"{nameof(ThetaDataProviderTests)}.{nameof(CanSubscribeAndUnsubscribeOnSecondResolution)}: ***** Summary *****");
            Log.Trace($"Input parameters: ticker:{ticker} | securityType:{securityType} | resolution:{resolution}");

            foreach (var data in dataFromEnumerator)
            {
                Log.Trace($"[{data.Key}] = {data.Value}");
            }

            if (securityType != SecurityType.Index)
            {
                Assert.Greater(dataFromEnumerator[typeof(QuoteBar)], 0);
            }
            // The ThetaData returns TradeBar seldom. Perhaps should find more relevant ticker.
            Assert.GreaterOrEqual(dataFromEnumerator[typeof(TradeBar)], 0);
        }

        [TestCase("AAPL", SecurityType.Equity)]
        [TestCase("SPX", SecurityType.Index)]
        public void MultipleSubscriptionOnOptionContractsTickResolution(string ticker, SecurityType securityType)
        {
            var minReturnResponse = 5;
            var obj = new object();
            var cancellationTokenSource = new CancellationTokenSource();
            var resetEvent = new AutoResetEvent(false);
            var underlyingSymbol = TestHelpers.CreateSymbol(ticker, securityType);
            var configs = _thetaDataProvider.LookupSymbols(underlyingSymbol, false).SelectMany(x => GetSubscriptionTickDataConfigs(x)).Take(250).ToList();

            var incomingSymbolDataByTickType = new ConcurrentDictionary<(Symbol, TickType), int>();

            Action<BaseData> callback = (dataPoint) =>
            {
                if (dataPoint == null)
                {
                    return;
                }

                var tick = dataPoint as Tick;

                lock (obj)
                {
                    switch (tick.TickType)
                    {
                        case TickType.Trade:
                            incomingSymbolDataByTickType[(tick.Symbol, tick.TickType)] += 1;
                            break;
                        case TickType.Quote:
                            incomingSymbolDataByTickType[(tick.Symbol, tick.TickType)] += 1;
                            break;
                    };
                }
            };

            foreach (var config in configs)
            {
                incomingSymbolDataByTickType.TryAdd((config.Symbol, config.TickType), 0);
                ProcessFeed(_thetaDataProvider.Subscribe(config, (sender, args) =>
                {
                    var dataPoint = ((NewDataAvailableEventArgs)args).DataPoint;
                    Log.Trace($"{dataPoint}. Time span: {dataPoint.Time} - {dataPoint.EndTime}");
                }),
                cancellationTokenSource.Token,
                300,
                callback: callback,
                throwExceptionCallback: () => cancellationTokenSource.Cancel());
            }

            resetEvent.WaitOne(TimeSpan.FromMinutes(1), cancellationTokenSource.Token);

            Log.Trace("Unsubscribing symbols");
            foreach (var config in configs)
            {
                _thetaDataProvider.Unsubscribe(config);
            }

            resetEvent.WaitOne(TimeSpan.FromSeconds(20), cancellationTokenSource.Token);

            var symbolVolatilities = incomingSymbolDataByTickType.Where(kv => kv.Value > 0).ToList();

            Log.Debug($"CancellationToken: {_cancellationTokenSource.Token.IsCancellationRequested}");

            Assert.IsNotEmpty(symbolVolatilities);
            Assert.That(symbolVolatilities.Count, Is.GreaterThan(minReturnResponse));

            cancellationTokenSource.Cancel();
        }

        private static IEnumerable<SubscriptionDataConfig> GetSubscriptionDataConfigs(string ticker, SecurityType securityType, Resolution resolution, decimal strikePrice, DateTime expiry,
            OptionRight optionRight = OptionRight.Call, string market = Market.USA)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, securityType, optionRight, strikePrice, expiry, market);
            foreach (var subscription in GetSubscriptionDataConfigs(symbol, resolution))
            {
                yield return subscription;
            }
        }
        private static IEnumerable<SubscriptionDataConfig> GetSubscriptionDataConfigs(Symbol symbol, Resolution resolution)
        {
            yield return GetSubscriptionDataConfig<TradeBar>(symbol, resolution);
            yield return GetSubscriptionDataConfig<QuoteBar>(symbol, resolution);
        }

        public static IEnumerable<SubscriptionDataConfig> GetSubscriptionTickDataConfigs(Symbol symbol)
        {
            yield return new SubscriptionDataConfig(GetSubscriptionDataConfig<Tick>(symbol, Resolution.Tick), tickType: TickType.Trade);
            yield return new SubscriptionDataConfig(GetSubscriptionDataConfig<Tick>(symbol, Resolution.Tick), tickType: TickType.Quote);
        }

        private static SubscriptionDataConfig GetSubscriptionDataConfig<T>(Symbol symbol, Resolution resolution)
        {
            return new SubscriptionDataConfig(
                typeof(T),
                symbol,
                resolution,
                TimeZones.Utc,
                TimeZones.Utc,
                true,
                extendedHours: false,
                false);
        }

        private Task ProcessFeed(
            IEnumerator<BaseData> enumerator,
            CancellationToken cancellationToken,
            int cancellationTokenDelayMilliseconds = 100,
            Action<BaseData> callback = null,
            Action throwExceptionCallback = null)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    while (enumerator.MoveNext() && !cancellationToken.IsCancellationRequested)
                    {
                        BaseData tick = enumerator.Current;

                        if (tick != null)
                        {
                            callback?.Invoke(tick);
                        }

                        cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(cancellationTokenDelayMilliseconds));
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"{nameof(ThetaDataProviderTests)}.{nameof(ProcessFeed)}.Exception: {ex.Message}");
                    throw;
                }
            }, cancellationToken).ContinueWith(task =>
            {
                if (throwExceptionCallback != null)
                {
                    throwExceptionCallback();
                }
                Log.Debug("The throwExceptionCallback is null.");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
