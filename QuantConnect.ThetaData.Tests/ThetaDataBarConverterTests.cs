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
using NUnit.Framework.Legacy;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using System.Collections.Generic;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    /// <summary>
    /// Zero and missing values in v3 rows. A row with no observation gives no data. No price is invented.
    /// </summary>
    [TestFixture]
    public class ThetaDataBarConverterTests
    {
        private static readonly Symbol Spy = Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        private static readonly Symbol Contract = Symbol.CreateOption(Spy, Market.USA, OptionStyle.American, OptionRight.Put, 760m, new DateTime(2026, 10, 20));

        private static EndOfDayRow Eod(decimal open, decimal high, decimal low, decimal close, decimal volume, long count, decimal bid, decimal bidSize, decimal ask, decimal askSize)
            => new()
            {
                Created = new DateTime(2026, 10, 20, 17, 15, 44, 979),
                LastTrade = new DateTime(2026, 10, 20, 16, 14, 39, 226),
                Open = open, High = high, Low = low, Close = close, Volume = volume, Count = count,
                Bid = bid, BidSize = bidSize, Ask = ask, AskSize = askSize
            };

        [Test]
        public void DailyTradeBarUsesTheTradingDayAndExactPrices()
        {
            var bar = ThetaDataBarConverter.ToDailyTradeBar(Eod(0.05m, 0.31m, 0.01m, 0.01m, 1616m, 254, 0m, 0m, 0.01m, 380m), Contract, TimeZones.NewYork)!;

            Assert.AreEqual(new DateTime(2026, 10, 20), bar.Time);
            Assert.AreEqual(Time.OneDay, bar.Period);
            Assert.AreEqual(0.05m, bar.Open);
            Assert.AreEqual(0.31m, bar.High);
            Assert.AreEqual(0.01m, bar.Low);
            Assert.AreEqual(0.01m, bar.Close);
            Assert.AreEqual(1616m, bar.Volume);
        }

        [Test]
        public void DailyTradeBarIsNullWhenTheContractDidNotTrade()
        {
            // As ThetaData sends it: "there were no qualifying trades during that interval".
            Assert.IsNull(ThetaDataBarConverter.ToDailyTradeBar(Eod(0m, 0m, 0m, 0m, 0m, 0, 153.38m, 200m, 153.93m, 200m), Contract, TimeZones.NewYork));
        }

        [Test]
        public void IndexDailyTradeBarIsKeptWithZeroVolumeAndCount()
        {
            // SPX rows have count 0 and volume 0 but real prices.
            var spx = Symbol.Create("SPX", SecurityType.Index, Market.USA);
            var bar = ThetaDataBarConverter.ToDailyTradeBar(Eod(4745.20m, 4754.33m, 4722.67m, 4742.83m, 0m, 0, 0m, 0m, 0m, 0m), spx, TimeZones.NewYork);

            Assert.IsNotNull(bar);
            Assert.AreEqual(4742.83m, bar!.Close);
            Assert.AreEqual(0m, bar.Volume);
        }

        [Test]
        public void DailyQuoteBarIsKeptWhenTheContractDidNotTrade()
        {
            var bar = ThetaDataBarConverter.ToDailyQuoteBar(Eod(0m, 0m, 0m, 0m, 0m, 0, 153.38m, 200m, 153.93m, 200m), Contract, TimeZones.NewYork)!;

            Assert.AreEqual(153.38m, bar.Bid.Close);
            Assert.AreEqual(153.93m, bar.Ask.Close);
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(0, 200, 0, 200)]
        [TestCase(1.5, 0, 0, 0)]
        [TestCase(0, 0, 0.01, 380)]
        public void DailyQuoteBarIsNullWithoutBothSides(decimal bid, decimal bidSize, decimal ask, decimal askSize)
        {
            Assert.IsNull(ThetaDataBarConverter.ToDailyQuoteBar(Eod(0m, 0m, 0m, 0m, 0m, 0, bid, bidSize, ask, askSize), Contract, TimeZones.NewYork));
        }

        [Test]
        public void IntradayBarsSkipEmptyIntervals()
        {
            var start = new DateTime(2026, 10, 20, 9, 30, 0);
            Assert.IsNull(ThetaDataBarConverter.ToTradeBar(new OhlcRow { Timestamp = start }, Contract, Time.OneMinute, TimeZones.NewYork));

            var trade = ThetaDataBarConverter.ToTradeBar(new OhlcRow { Timestamp = start, Open = 1m, High = 2m, Low = 0.5m, Close = 1.5m, Volume = 0m, Count = 1 }, Contract, Time.OneMinute, TimeZones.NewYork)!;
            Assert.AreEqual(start, trade.Time);
            Assert.AreEqual(start.AddMinutes(1), trade.EndTime);

            var quote = ThetaDataBarConverter.ToQuoteBar(new QuoteRow { Timestamp = start, Bid = 0.04m, BidSize = 5m, Ask = 0.05m, AskSize = 10m }, Contract, Time.OneMinute, TimeZones.NewYork)!;
            Assert.AreEqual(0.04m, quote.Bid.Close);
            Assert.AreEqual(0.05m, quote.Ask.Close);
            Assert.AreEqual(start.AddMinutes(1), quote.EndTime);

            Assert.IsNull(ThetaDataBarConverter.ToQuoteBar(new QuoteRow { Timestamp = start }, Contract, Time.OneMinute, TimeZones.NewYork));
        }

        [Test]
        public void TicksSkipRowsWithoutAPrice()
        {
            var time = new DateTime(2026, 10, 20, 9, 30, 0, 123);
            Assert.IsNull(ThetaDataBarConverter.ToTradeTick(new TradeRow { Timestamp = time, Price = 0m, Size = 1m }, Contract, TimeZones.NewYork));
            Assert.IsNull(ThetaDataBarConverter.ToQuoteTick(new QuoteRow { Timestamp = time }, Contract, TimeZones.NewYork));

            var tick = ThetaDataBarConverter.ToQuoteTick(new QuoteRow { Timestamp = time, Bid = 1.1m, BidSize = 5m, Ask = 1.2m, AskSize = 3m }, Contract, TimeZones.NewYork)!;
            Assert.AreEqual(1.1m, tick.BidPrice);
            Assert.AreEqual(1.2m, tick.AskPrice);
            Assert.AreEqual(time, tick.Time);
        }

        [Test]
        public void ConvertsEasternTimeToTheExchangeTimeZone()
        {
            // 2026-10-20 is in daylight saving time: 09:30 ET is 13:30 UTC.
            var bar = ThetaDataBarConverter.ToTradeBar(new OhlcRow { Timestamp = new DateTime(2026, 10, 20, 9, 30, 0), Open = 1, High = 1, Low = 1, Close = 1, Count = 1 },
                Contract, Time.OneMinute, TimeZones.Utc)!;
            Assert.AreEqual(new DateTime(2026, 10, 20, 13, 30, 0), bar.Time);

            // 2026-12-01 is in standard time: 09:30 ET is 14:30 UTC.
            bar = ThetaDataBarConverter.ToTradeBar(new OhlcRow { Timestamp = new DateTime(2026, 12, 1, 9, 30, 0), Open = 1, High = 1, Low = 1, Close = 1, Count = 1 },
                Contract, Time.OneMinute, TimeZones.Utc)!;
            Assert.AreEqual(new DateTime(2026, 12, 1, 14, 30, 0), bar.Time);
        }

        [Test]
        public void OrdersBarsAndRemovesDuplicates()
        {
            var day1 = new TradeBar(new DateTime(2026, 10, 19), Spy, 1, 1, 1, 1, 1, Time.OneDay);
            var day2 = new TradeBar(new DateTime(2026, 10, 20), Spy, 2, 2, 2, 2, 2, Time.OneDay);
            var day2Again = new TradeBar(new DateTime(2026, 10, 20), Spy, 3, 3, 3, 3, 3, Time.OneDay);

            var result = ThetaDataBarConverter.OrderAndRemoveDuplicates(new List<BaseData> { day2, day1, day2Again }, Resolution.Daily).ToList();

            CollectionAssert.AreEqual(new[] { day1, day2 }, result);
        }

        [Test]
        public void KeepsTicksWithTheSameTimestamp()
        {
            var time = new DateTime(2026, 10, 20, 9, 30, 0);
            var ticks = new List<BaseData> { new Tick(time, Spy, "", "", 1, 10m), new Tick(time, Spy, "", "", 2, 10m) };

            Assert.AreEqual(2, ThetaDataBarConverter.OrderAndRemoveDuplicates(ticks, Resolution.Tick).Count());
        }
    }
}
