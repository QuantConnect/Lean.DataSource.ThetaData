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
using System.IO;
using System.Linq;
using System.Net.Http;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using QuantConnect.Configuration;
using System.Collections.Generic;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    /// <summary>
    /// The history provider and chain provider on recorded v3 responses of the Free plan. No Theta Terminal is needed.
    /// The provider checks the QuantConnect module license when it starts, as before, so these tests need the
    /// QC_JOB_USER_ID and QC_API_ACCESS_TOKEN environment variables that CI sets.
    /// </summary>
    [TestFixture]
    public class ThetaDataV3HistoryTests
    {
        private static readonly Symbol Spy = Symbol.Create("SPY", SecurityType.Equity, Market.USA);
        private static readonly Symbol CanonicalSpy = Symbol.CreateCanonicalOption(Spy);
        private const string FreePlanDenial = "Requesting an option endpoint requiring a value subscription, but you only have a FREE subscription. Please consider upgrading! ( https://www.thetadata.net/subscribe )";

        /// <summary>
        /// Answers like a Free plan terminal that has data for 2026-10-19 and 2026-10-20 only.
        /// </summary>
        private static HttpResponseMessage FreePlanTerminal(FakeThetaDataHandler.FakeRequest request)
        {
            var start = request.Query["start_date"] ?? request.Query["date"];
            switch (request.Path)
            {
                case "option/list/contracts/quote":
                case "option/history/open_interest":
                    return FakeThetaDataHandler.Respond(403, FreePlanDenial);
                case "stock/history/eod" when string.CompareOrdinal(start, "20261020") <= 0:
                    return FakeThetaDataHandler.Fixture("stock_eod_spy_20261019_20261020.json");
                case "index/history/eod" when string.CompareOrdinal(start, "20261020") <= 0:
                    return FakeThetaDataHandler.Fixture("index_eod_spx_20261019_20261020.json");
                case "option/history/eod" when request.Query["expiration"] == "*" && start == "20261020":
                    return FakeThetaDataHandler.Fixture("option_eod_spy_bulk_20261020_sample.json");
                case "option/history/eod" when request.Query["expiration"] == "20240119":
                    return FakeThetaDataHandler.Fixture("option_eod_spy_20240119_320c_20240102_20240103.json");
                default:
                    return FakeThetaDataHandler.Respond(472, "No data found for your request");
            }
        }

        private static (ThetaDataProvider Provider, FakeThetaDataHandler Handler) CreateProvider(Func<FakeThetaDataHandler.FakeRequest, HttpResponseMessage>? respond = null, string plan = "Free")
        {
            var handler = new FakeThetaDataHandler(respond ?? FreePlanTerminal);
            var client = new ThetaDataRestApiClient("http://127.0.0.1:25503", null, 1, handler, _ => TimeSpan.Zero, 1);
            return (new ThetaDataProvider(plan, client), handler);
        }

        private static HistoryRequest CreateRequest(Symbol symbol, Type dataType, TickType tickType, DateTime startLocal, DateTime endLocal, Resolution resolution = Resolution.Daily)
        {
            var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);
            return new HistoryRequest(startLocal.ConvertToUtc(exchangeHours.TimeZone), endLocal.ConvertToUtc(exchangeHours.TimeZone), dataType, symbol, resolution,
                exchangeHours, exchangeHours.TimeZone, resolution, true, false, DataNormalizationMode.Raw, tickType);
        }

        [Test]
        public void GetsEquityDailyTradeBars()
        {
            var (provider, handler) = CreateProvider();
            using var _ = provider;

            var bars = provider.GetHistory(CreateRequest(Spy, typeof(TradeBar), TickType.Trade, new DateTime(2026, 10, 19), new DateTime(2026, 10, 21)))!.Cast<TradeBar>().ToList();

            Assert.AreEqual(2, bars.Count);
            Assert.AreEqual(new DateTime(2026, 10, 19), bars[0].Time);
            Assert.AreEqual(new DateTime(2026, 10, 20), bars[1].Time);
            var expected = ThetaDataRestApiClient.ParseRows<EndOfDayRow>(FakeThetaDataHandler.ReadFixture("stock_eod_spy_20261019_20261020.json"));
            Assert.AreEqual(expected[1].Close, bars[1].Close);
            Assert.AreEqual(expected[1].Volume, bars[1].Volume);

            var request = handler.Requests.Single();
            Assert.AreEqual("stock/history/eod", request.Path);
            Assert.AreEqual("SPY", request.Query["symbol"]);
            // The request ends at 2026-10-21 00:00 ET, so it holds no data of 2026-10-21.
            Assert.AreEqual("20261019", request.Query["start_date"]);
            Assert.AreEqual("20261020", request.Query["end_date"]);
        }

        [Test]
        public void ReturnsTheDailyBarOfTheDayForAUtcMidnightWindow()
        {
            // lean data download --start 20261020 --end 20261021 asks for 2026-10-20 00:00 UTC to 2026-10-21 00:00 UTC.
            // That is 2026-10-19 20:00 to 2026-10-20 20:00 ET, and it must return the bar of 2026-10-20.
            var (provider, _) = CreateProvider();
            using var __ = provider;
            var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Spy, SecurityType.Equity);
            var request = new HistoryRequest(new DateTime(2026, 10, 20), new DateTime(2026, 10, 21), typeof(TradeBar), Spy, Resolution.Daily,
                exchangeHours, exchangeHours.TimeZone, Resolution.Daily, true, false, DataNormalizationMode.Raw, TickType.Trade);

            var history = provider.GetHistory(request)!.ToList();

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(new DateTime(2026, 10, 20), history[0].Time);
        }

        [Test]
        public void ClampsTheStartToTheFirstEasternDateOfThePlan()
        {
            // LEAN asks for SPY daily data from its first date, 1998-01-02. The Free plan starts on 2023-06-01 (ET):
            // a request for 2023-05-31 is denied with HTTP 403 for the whole range.
            var (provider, handler) = CreateProvider();
            using var _ = provider;
            var exchangeHours = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, Spy, SecurityType.Equity);
            var request = new HistoryRequest(new DateTime(1998, 1, 2), new DateTime(2026, 10, 27), typeof(TradeBar), Spy, Resolution.Daily,
                exchangeHours, exchangeHours.TimeZone, Resolution.Daily, true, false, DataNormalizationMode.Raw, TickType.Trade);

            Assert.AreEqual(2, provider.GetHistory(request)!.Count());
            // 1998 to 2026 is clamped to 2023-06-01, then split into requests of at most 365 days.
            Assert.AreEqual("20230601", handler.Requests.First().Query["start_date"]);
            Assert.AreEqual(4, handler.Requests.Count);
        }

        [Test]
        public void GetsOptionContractDailyDataWithV3Parameters()
        {
            var (provider, handler) = CreateProvider();
            using var _ = provider;
            var contract = Symbol.CreateOption(Spy, Market.USA, OptionStyle.American, OptionRight.Call, 320m, new DateTime(2024, 1, 19));

            var trades = provider.GetHistory(CreateRequest(contract, typeof(TradeBar), TickType.Trade, new DateTime(2024, 1, 2), new DateTime(2024, 1, 4)))!.Cast<TradeBar>().ToList();
            var quotes = provider.GetHistory(CreateRequest(contract, typeof(QuoteBar), TickType.Quote, new DateTime(2024, 1, 2), new DateTime(2024, 1, 4)))!.Cast<QuoteBar>().ToList();

            // 2024-01-02 had no trade: no trade bar, but a quote bar.
            Assert.AreEqual(1, trades.Count);
            Assert.AreEqual(new DateTime(2024, 1, 3), trades[0].Time);
            Assert.AreEqual(151.08m, trades[0].Close);
            Assert.AreEqual(2, quotes.Count);
            Assert.AreEqual(153.38m, quotes[0].Bid.Close);
            Assert.AreEqual(153.93m, quotes[0].Ask.Close);

            var request = handler.Requests.First();
            Assert.AreEqual("option/history/eod", request.Path);
            Assert.AreEqual("SPY", request.Query["symbol"]);
            Assert.AreEqual("20240119", request.Query["expiration"]);
            Assert.AreEqual("320.000", request.Query["strike"]);
            Assert.AreEqual("call", request.Query["right"]);
            Assert.IsNull(request.Query["root"]);
            Assert.IsNull(request.Query["exp"]);
        }

        [Test]
        public void ListsTheChainFromTheEndOfDayReportOnTheFreePlan()
        {
            var (provider, handler) = CreateProvider();
            using var _ = provider;

            var first = provider.GetOptionChain(Spy, new DateTime(2026, 10, 20)).ToList();
            var second = provider.GetOptionChain(CanonicalSpy, new DateTime(2026, 10, 20)).ToList();

            Assert.AreEqual(6, first.Count);
            CollectionAssert.AreEquivalent(first, second);
            Assert.That(first.All(x => x.SecurityType == SecurityType.Option && x.Underlying == Spy && x.ID.OptionStyle == OptionStyle.American));
            Assert.AreEqual(1, handler.Requests.Count(x => x.Path == "option/list/contracts/quote"), "the denied request is not sent again");
        }

        [Test]
        public void ReturnsNoDataWhenThePlanDeniesIt()
        {
            var (provider, _) = CreateProvider();
            using var __ = provider;
            var contract = Symbol.CreateOption(Spy, Market.USA, OptionStyle.American, OptionRight.Call, 320m, new DateTime(2024, 1, 19));

            var history = provider.GetHistory(CreateRequest(contract, typeof(OpenInterest), TickType.OpenInterest, new DateTime(2024, 1, 2), new DateTime(2024, 1, 4)));

            Assert.IsNull(history);
        }

        [Test]
        public void ThrowsWhenTheTerminalFails()
        {
            // A failed request must not look like an empty result.
            var (provider, _) = CreateProvider(_ => throw new HttpRequestException("Connection refused"));
            using var __ = provider;

            Assert.Throws<ThetaDataRequestException>(() => provider.GetHistory(CreateRequest(Spy, typeof(TradeBar), TickType.Trade, new DateTime(2026, 10, 19), new DateTime(2026, 10, 21))));
        }

    }
}
