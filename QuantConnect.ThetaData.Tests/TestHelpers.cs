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
using QuantConnect.Tests;
using Microsoft.CodeAnalysis;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    public static class TestHelpers
    {
        /// <summary>
        /// Represents the time zone used by ThetaData, which returns time in the New York (EST) Time Zone with daylight savings time.
        /// </summary>
        /// <remarks>
        /// <see href="https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/ke230k18g7fld-trading-hours"/>
        /// </remarks>
        private static DateTimeZone TimeZoneThetaData = TimeZones.NewYork;

        public static void ValidateHistoricalBaseData(IEnumerable<BaseData> history, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate, Symbol requestedSymbol = null)
        {
            Assert.IsNotNull(history);
            Assert.IsNotEmpty(history);

            if (resolution < Resolution.Daily)
            {
                Assert.That(history.First().Time.Date, Is.EqualTo(startDate.ConvertFromUtc(TimeZoneThetaData).Date));
                Assert.That(history.Last().Time.Date, Is.EqualTo(endDate.ConvertFromUtc(TimeZoneThetaData).Date));
            }
            else
            {
                Assert.That(history.First().Time.Date, Is.GreaterThanOrEqualTo(startDate.ConvertFromUtc(TimeZoneThetaData).Date));
                Assert.That(history.Last().Time.Date, Is.LessThanOrEqualTo(endDate.ConvertFromUtc(TimeZoneThetaData).Date));
            }

            switch (tickType)
            {
                case TickType.Trade when resolution != Resolution.Tick:
                    AssertTradeBars(history.Select(x => x as TradeBar), requestedSymbol, resolution.ToTimeSpan());
                    break;
                case TickType.Trade when requestedSymbol.SecurityType != SecurityType.Index:
                    AssertTradeTickBars(history.Select(x => x as Tick), requestedSymbol);
                    break;
                case TickType.Trade when requestedSymbol.SecurityType == SecurityType.Index:
                    AssertIndexTradeTickBars(history.Select(x => x as Tick), requestedSymbol);
                    break;
                case TickType.Quote when resolution == Resolution.Tick:
                    AssertQuoteTickBars(history.Select(x => x as Tick), requestedSymbol);
                    break;
                case TickType.Quote:
                    AssertQuoteBars(history.Select(t => t as QuoteBar), requestedSymbol, resolution.ToTimeSpan());
                    break;
            }
        }

        public static void AssertTradeTickBars(IEnumerable<Tick> ticks, Symbol symbol = null)
        {
            foreach (var tick in ticks)
            {
                if (symbol != null)
                {
                    Assert.That(tick.Symbol, Is.EqualTo(symbol));
                }

                Assert.That(tick.Price, Is.GreaterThan(0));
                Assert.That(tick.Value, Is.GreaterThan(0));
                Assert.IsNotEmpty(tick.SaleCondition);
            }
        }

        public static void AssertIndexTradeTickBars(IEnumerable<Tick> ticks, Symbol symbol = null)
        {
            foreach (var tick in ticks)
            {
                if (symbol != null)
                {
                    Assert.That(tick.Symbol, Is.EqualTo(symbol));
                }

                Assert.That(tick.Price, Is.GreaterThan(0));
            }
        }

        public static void AssertQuoteTickBars(IEnumerable<Tick> ticks, Symbol symbol = null)
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

        public static void AssertQuoteBars(IEnumerable<QuoteBar> ticks, Symbol symbol, TimeSpan resolutionInTimeSpan)
        {
            foreach (var tick in ticks)
            {
                if (symbol != null)
                {
                    Assert.That(tick.Symbol, Is.EqualTo(symbol));
                }

                Assert.That(tick.Ask.Open, Is.GreaterThan(0));
                Assert.That(tick.Ask.High, Is.GreaterThan(0));
                Assert.That(tick.Ask.Low, Is.GreaterThan(0));
                Assert.That(tick.Ask.Close, Is.GreaterThan(0));

                Assert.That(tick.Bid.Open, Is.GreaterThan(0));
                Assert.That(tick.Bid.High, Is.GreaterThan(0));
                Assert.That(tick.Bid.Low, Is.GreaterThan(0));
                Assert.That(tick.Bid.Close, Is.GreaterThan(0));

                Assert.That(tick.Close, Is.GreaterThan(0));
                Assert.That(tick.High, Is.GreaterThan(0));
                Assert.That(tick.Low, Is.GreaterThan(0));
                Assert.That(tick.Open, Is.GreaterThan(0));
                Assert.That(tick.Price, Is.GreaterThan(0));
                Assert.That(tick.Value, Is.GreaterThan(0));
                Assert.That(tick.DataType, Is.EqualTo(MarketDataType.QuoteBar));
                Assert.That(tick.EndTime, Is.GreaterThan(default(DateTime)));
                Assert.That(tick.Time, Is.GreaterThan(default(DateTime)));

                Assert.That(tick.LastAskSize, Is.GreaterThan(0));
                Assert.That(tick.LastBidSize, Is.GreaterThan(0));
                Assert.That(tick.Period, Is.EqualTo(resolutionInTimeSpan));
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
                Assert.That(tradeBar.Volume, Is.GreaterThanOrEqualTo(0));
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
                DataNormalizationMode.Raw,
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
                case SecurityType.FutureOption:
                    var underlyingFuture = Symbols.CreateFutureSymbol(ticker, expirationDate.Value);
                    return Symbols.CreateFutureOptionSymbol(underlyingFuture, optionRight.Value, strikePrice.Value, expirationDate.Value);
                default:
                    throw new NotSupportedException($"The security type '{securityType}' is not supported.");
            }
        }
    }
}
