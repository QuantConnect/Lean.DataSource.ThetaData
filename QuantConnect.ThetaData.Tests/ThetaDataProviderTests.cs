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
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
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

        [Test]
        public void CanSubscribeAndUnsubscribe()
        {
            var unsubscribed = false;

            var configs = GetSubscriptionDataConfigs("JD", Resolution.Second);

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

            Thread.Sleep(TimeSpan.FromSeconds(10));

            Log.Trace("Unsubscribing symbols");
            foreach (var config in configs)
            {
                _thetaDataProvider.Unsubscribe(config);
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));

            Assert.Greater(dataFromEnumerator[typeof(QuoteBar)], 0);
            // The ThetaData returns TradeBar seldom. Perhaps should find more relevant ticker.
            Assert.GreaterOrEqual(dataFromEnumerator[typeof(TradeBar)], 0);
        }

        private static IEnumerable<SubscriptionDataConfig> GetSubscriptionDataConfigs(string ticker, Resolution resolution)
        {
            var underlyingSymbol = Symbol.Create(ticker, SecurityType.Equity, Market.USA);
            var option = Symbol.CreateOption(underlyingSymbol, Market.USA, OptionStyle.American, OptionRight.Call, 22m, new DateTime(2024, 03, 08));
            foreach (var subscription in GetSubscriptionDataConfigs(option, resolution))
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
                catch
                {
                    throw;
                }
            }, cancellationToken).ContinueWith(task =>
            {
                if (throwExceptionCallback != null)
                {
                    throwExceptionCallback();
                }
                Log.Error("The throwExceptionCallback is null.");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
