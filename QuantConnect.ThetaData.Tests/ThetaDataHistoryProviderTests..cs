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
using NodaTime;
using System.Linq;
using NUnit.Framework;
using QuantConnect.Util;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    public class ThetaDataHistoryProviderTests
    {
        ThetaDataProvider _thetaDataProvider = new();


        [TestCase("AAPL", SecurityType.Option, Resolution.Hour, TickType.OpenInterest, "2024/03/18", "2024/03/28", Description = "Wrong Resolution for OpenInterest")]
        [TestCase("AAPL", SecurityType.Option, Resolution.Hour, TickType.OpenInterest, "2024/03/28", "2024/03/18", Description = "StartDate > EndDate")]
        //[TestCase("AAPL", SecurityType.Equity, Resolution.Hour, TickType.OpenInterest, "2024/03/28", "2024/03/18", Description = "Wrong SecurityType")]
        public void TryGetHistoryDataWithInvalidRequestedParameters(string ticker, SecurityType securityType, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, securityType, OptionRight.Call, 170, new DateTime(2024, 03, 28));

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest)?.ToList();

            Assert.IsNull(history);
        }

        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Daily, TickType.Trade, "2024/01/18", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Daily, TickType.OpenInterest, "2024/01/18", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Tick, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Second, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Hour, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Daily, TickType.Quote, "2024/01/18", "2024/03/28")]
        public void GetHistoryOptionData(string ticker, OptionRight optionRight, decimal strikePrice, DateTime expirationDate, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.Option, optionRight, strikePrice, expirationDate);

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            Assert.IsNotEmpty(history);

            if (resolution < Resolution.Daily)
            {
                Assert.That(history.First().Time.Date, Is.EqualTo(startDate.ConvertFromUtc(TimeZones.EasternStandard).Date));
                Assert.That(history.Last().Time.Date, Is.EqualTo(endDate.ConvertFromUtc(TimeZones.EasternStandard).Date));
            }
            else
            {
                Assert.That(history.First().Time.Date, Is.GreaterThanOrEqualTo(startDate.ConvertFromUtc(TimeZones.EasternStandard).Date));
                Assert.That(history.Last().Time.Date, Is.LessThanOrEqualTo(endDate.ConvertFromUtc(TimeZones.EasternStandard).Date));
            }
            
            switch (tickType)
            {
                case TickType.Trade:
                    AssertTradeBars(history.Select(x => x as TradeBar), symbol, resolution.ToTimeSpan());
                    break;
                case TickType.Quote:
                    AssertTickBars(history.Select(t => t as Tick), symbol);
                    break;
            }
        }

        private void AssertTickBars(IEnumerable<Tick> ticks, Symbol symbol)
        {
            foreach (var tick in ticks)
            {
                Assert.That(tick.Symbol, Is.EqualTo(symbol));
                Assert.That(tick.AskPrice, Is.GreaterThan(0));
                Assert.That(tick.AskSize, Is.GreaterThan(0));
                Assert.That(tick.BidPrice, Is.GreaterThan(0));
                Assert.That(tick.BidSize, Is.GreaterThan(0));
                Assert.That(tick.DataType, Is.EqualTo(MarketDataType.Tick));
                Assert.That(tick.Time, Is.GreaterThan(default(DateTime)));
                Assert.That(tick.EndTime, Is.GreaterThan(default(DateTime)));
                Assert.IsNotEmpty(tick.SaleCondition);
            }
        }

        private void AssertTradeBars(IEnumerable<TradeBar> tradeBars, Symbol symbol, TimeSpan period)
        {
            foreach (var tradeBar in tradeBars)
            {
                Assert.That(tradeBar.Symbol, Is.EqualTo(symbol));
                Assert.That(tradeBar.Period, Is.EqualTo(period));
                Assert.That(tradeBar.Open, Is.GreaterThan(0));
                Assert.That(tradeBar.High, Is.GreaterThan(0));
                Assert.That(tradeBar.Low, Is.GreaterThan(0));
                Assert.That(tradeBar.Close, Is.GreaterThan(0));
                Assert.That(tradeBar.Price, Is.GreaterThan(0));
                Assert.That(tradeBar.Volume, Is.GreaterThan(0));
                Assert.That(tradeBar.Time, Is.GreaterThan(default(DateTime)));
                Assert.That(tradeBar.EndTime, Is.GreaterThan(default(DateTime)));
            }
        }
    }
}
