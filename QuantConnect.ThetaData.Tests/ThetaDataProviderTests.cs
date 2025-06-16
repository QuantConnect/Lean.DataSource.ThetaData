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
using System.Text;
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

        private static IEnumerable<TestCaseData> TestParameters
        {
            get
            {
                var AAPL = Symbols.AAPL;
                yield return new TestCaseData(new Symbol[] { AAPL }, Resolution.Second);

                var DJI_Index = Symbol.Create("DJI", SecurityType.Index, Market.USA);
                yield return new TestCaseData(new Symbol[] { DJI_Index }, Resolution.Second);

                var NVDA = Symbol.Create("NVDA", SecurityType.Equity, Market.USA);
                var DJT = Symbol.Create("DJT", SecurityType.Equity, Market.USA);
                var TSLA = Symbol.Create("TSLA", SecurityType.Equity, Market.USA);
                yield return new TestCaseData(new Symbol[] { AAPL, DJI_Index, NVDA, DJT, TSLA }, Resolution.Second);

                var AAPL_Option = Symbol.CreateOption(AAPL, AAPL.ID.Market, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 220m, new DateTime(2025, 06, 20));
                var AAPL_Option2 = Symbol.CreateOption(AAPL, AAPL.ID.Market, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 217.5m, new DateTime(2026, 06, 20));
                var AAPL_Option3 = Symbol.CreateOption(AAPL, AAPL.ID.Market, SecurityType.Option.DefaultOptionStyle(), OptionRight.Put, 220m, new DateTime(2026, 06, 20));
                var AAPL_Option4 = Symbol.CreateOption(AAPL, AAPL.ID.Market, SecurityType.Option.DefaultOptionStyle(), OptionRight.Put, 217.5m, new DateTime(2026, 06, 20));
                yield return new TestCaseData(new[] { AAPL_Option, AAPL_Option2, AAPL_Option3, AAPL_Option4 }, Resolution.Second);

                var nok = Symbol.Create("NOK", SecurityType.Equity, Market.USA);
                var nok_option = Symbol.CreateOption(nok, nok.ID.Market, SecurityType.Option.DefaultOptionStyle(), OptionRight.Call, 7.5m, new DateTime(2026, 06, 20));

                yield return new TestCaseData(new[] { nok_option }, Resolution.Second);
            }
        }

        [Test, TestCaseSource(nameof(TestParameters))]
        public void CanSubscribeAndUnsubscribeOnSecondResolution(Symbol[] symbols, Resolution resolution)
        {

            var configs = new List<SubscriptionDataConfig>();

            var dataFromEnumerator = new Dictionary<Symbol, Dictionary<Type, int>>();

            foreach (var symbol in symbols)
            {
                dataFromEnumerator[symbol] = new Dictionary<Type, int>();
                foreach (var config in GetSubscriptionDataConfigs(symbol, resolution))
                {
                    configs.Add(config);

                    var tickType = config.TickType switch
                    {
                        TickType.Quote => typeof(QuoteBar),
                        TickType.Trade => typeof(TradeBar),
                        _ => throw new NotImplementedException()
                    };

                    dataFromEnumerator[symbol][tickType] = 0;
                }
            }

            Assert.That(configs, Is.Not.Empty);

            Action<BaseData> callback = (dataPoint) =>
            {
                if (dataPoint == null)
                {
                    return;
                }

                switch (dataPoint)
                {
                    case TradeBar tb:
                        dataFromEnumerator[tb.Symbol][typeof(TradeBar)] += 1;
                        break;
                    case QuoteBar qb:
                        Assert.GreaterOrEqual(qb.Ask.Open, qb.Bid.Open, $"QuoteBar validation failed for {qb.Symbol}: Ask.Open ({qb.Ask.Open}) <= Bid.Open ({qb.Bid.Open}). Full data: {DisplayBaseData(qb)}");
                        Assert.GreaterOrEqual(qb.Ask.High, qb.Bid.High, $"QuoteBar validation failed for {qb.Symbol}: Ask.High ({qb.Ask.High}) <= Bid.High ({qb.Bid.High}). Full data: {DisplayBaseData(qb)}");
                        Assert.GreaterOrEqual(qb.Ask.Low, qb.Bid.Low, $"QuoteBar validation failed for {qb.Symbol}: Ask.Low ({qb.Ask.Low}) <= Bid.Low ({qb.Bid.Low}). Full data: {DisplayBaseData(qb)}");
                        Assert.GreaterOrEqual(qb.Ask.Close, qb.Bid.Close, $"QuoteBar validation failed for {qb.Symbol}: Ask.Close ({qb.Ask.Close}) <= Bid.Close ({qb.Bid.Close}). Full data: {DisplayBaseData(qb)}");
                        dataFromEnumerator[qb.Symbol][typeof(QuoteBar)] += 1;
                        break;
                }
                ;
            };

            foreach (var config in configs)
            {
                ProcessFeed(_thetaDataProvider.Subscribe(config, (sender, args) =>
                {
                    var dataPoint = ((NewDataAvailableEventArgs)args).DataPoint;
                    Log.Trace($"{dataPoint}. Time span: {dataPoint.Time} - {dataPoint.EndTime}");
                }), _cancellationTokenSource.Token, callback: callback);
            }

            Thread.Sleep(TimeSpan.FromSeconds(60));

            Log.Trace("Unsubscribing symbols");
            foreach (var config in configs)
            {
                _thetaDataProvider.Unsubscribe(config);
            }

            Thread.Sleep(TimeSpan.FromSeconds(5));

            _cancellationTokenSource.Cancel();

            var str = new StringBuilder();

            str.AppendLine($"{nameof(ThetaDataProviderTests)}.{nameof(CanSubscribeAndUnsubscribeOnSecondResolution)}: ***** Summary *****");

            foreach (var symbol in symbols)
            {
                str.AppendLine($"Input parameters: ticker:{symbol} | securityType:{symbol.SecurityType} | resolution:{resolution}");

                foreach (var tickType in dataFromEnumerator[symbol])
                {
                    str.AppendLine($"[{tickType.Key}] = {tickType.Value}");

                    if (symbol.SecurityType != SecurityType.Index)
                    {
                        Assert.Greater(tickType.Value, 0);
                    }
                    // The ThetaData returns TradeBar seldom. Perhaps should find more relevant ticker.
                    Assert.GreaterOrEqual(tickType.Value, 0);
                }
                str.AppendLine(new string('-', 30));
            }

            Log.Trace(str.ToString());
        }

        private static string DisplayBaseData(BaseData item)
        {
            switch (item)
            {
                case TradeBar tradeBar:
                    return $"Data Type: {item.DataType} | " + tradeBar.ToString() + $" Time: {tradeBar.Time}, EndTime: {tradeBar.EndTime}";
                default:
                    return $"DEFAULT: Data Type: {item.DataType} | Time: {item.Time} | End Time: {item.EndTime} | Symbol: {item.Symbol} | Price: {item.Price} | IsFillForward: {item.IsFillForward}";
            }
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
                    }
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
