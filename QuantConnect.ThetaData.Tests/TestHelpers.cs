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
using QuantConnect.Data;
using QuantConnect.Util;
using Microsoft.CodeAnalysis;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    public static class TestHelpers
    {
        public static void ValidateHistoricalBaseData(IEnumerable<BaseData> history, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate, Symbol requestedSymbol = null)
        {
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
                    AssertTradeBars(history.Select(x => x as TradeBar), requestedSymbol, resolution.ToTimeSpan());
                    break;
                case TickType.Quote:
                    AssertTickBars(history.Select(t => t as Tick), requestedSymbol);
                    break;
            }
        }

        public static void AssertTickBars(IEnumerable<Tick> ticks, Symbol symbol = null)
        {
            foreach (var tick in ticks)
            {
                if (symbol != null)
                {
                    Assert.That(tick.Symbol, Is.EqualTo(symbol));
                }

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

        public static void AssertTradeBars(IEnumerable<TradeBar> tradeBars, Symbol symbol, TimeSpan period)
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

        public static HistoryRequest CreateHistoryRequest(Symbol symbol, Resolution resolution, TickType tickType, DateTime startDateTime, DateTime endDateTime,
            SecurityExchangeHours exchangeHours = null, DateTimeZone dataTimeZone = null)
        {
            if (exchangeHours == null)
            {
                exchangeHours = SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork);
            }

            if (dataTimeZone == null)
            {
                dataTimeZone = TimeZones.NewYork;
            }

            var dataType = LeanData.GetDataType(resolution, tickType);
            return new HistoryRequest(
                startDateTime,
                endDateTime,
                dataType,
                symbol,
                resolution,
                exchangeHours,
                dataTimeZone,
                null,
                true,
                false,
                DataNormalizationMode.Adjusted,
                tickType
                );
        }

        public static Symbol CreateSymbol(string ticker, SecurityType securityType, OptionRight? optionRight = null, decimal? strikePrice = null, DateTime? expirationDate = null, string market = Market.USA)
        {
            switch (securityType)
            {
                case SecurityType.Equity:
                case SecurityType.Index:
                    return Symbol.Create(ticker, securityType, market);
                case SecurityType.Option:
                    var underlyingEquitySymbol = Symbol.Create(ticker, SecurityType.Equity, market);
                    return Symbol.CreateOption(underlyingEquitySymbol, market, OptionStyle.American, optionRight.Value, strikePrice.Value, expirationDate.Value);
                case SecurityType.IndexOption:
                    var underlyingIndexSymbol = Symbol.Create(ticker, SecurityType.Index, market);
                    return Symbol.CreateOption(underlyingIndexSymbol, market, OptionStyle.American, optionRight.Value, strikePrice.Value, expirationDate.Value);
                default:
                    throw new NotSupportedException($"The security type '{securityType}' is not supported.");
            }
        }
    }
}
