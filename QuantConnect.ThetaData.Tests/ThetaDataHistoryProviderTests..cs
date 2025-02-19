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
using QuantConnect.Data;
using System.Diagnostics;
using QuantConnect.Logging;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    [Explicit("This test requires the ThetaData terminal to be running in order to execute properly.")]
    public class ThetaDataHistoryProviderTests
    {
        ThetaDataProvider _thetaDataProvider = new();

        [TestCase("AAPL", SecurityType.Option, Resolution.Hour, TickType.OpenInterest, "2024/03/28", "2024/03/18", Description = "StartDate > EndDate")]
        [TestCase("AAPL", SecurityType.Equity, Resolution.Hour, TickType.OpenInterest, "2024/03/28", "2024/04/02", Description = "Wrong TickType")]
        [TestCase("AAPL", SecurityType.Equity, Resolution.Daily, TickType.Trade, "2024/07/07", "2024/07/08", Description = "Use Weekend data, No data return")]
        [TestCase("AAPL", SecurityType.FutureOption, Resolution.Hour, TickType.Trade, "2024/03/28", "2024/03/18", Description = "Wrong SecurityType")]
        [TestCase("SPY", SecurityType.Index, Resolution.Hour, TickType.OpenInterest, "2024/03/28", "2024/04/02", Description = "Wrong TickType, Index support only Trade")]
        [TestCase("SPY", SecurityType.Index, Resolution.Minute, TickType.Quote, "2024/03/28", "2024/04/02", Description = "Wrong TickType, Index support only Trade")]
        public void TryGetHistoryDataWithInvalidRequestedParameters(string ticker, SecurityType securityType, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, securityType, OptionRight.Call, 170, new DateTime(2024, 03, 28));

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest);

            Assert.IsNull(history);
        }

        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Daily, TickType.Trade, "2024/01/18", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Daily, TickType.OpenInterest, "2024/01/18", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Daily, TickType.Quote, "2024/01/18", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Tick, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Tick, TickType.Trade, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Second, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Put, 170, "2024/03/28", Resolution.Second, TickType.Trade, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Hour, TickType.Quote, "2024/03/19", "2024/03/28")]
        [TestCase("AAPL", OptionRight.Call, 170, "2024/03/28", Resolution.Hour, TickType.Trade, "2024/03/19", "2024/03/28")]
        public void GetHistoryOptionData(string ticker, OptionRight optionRight, decimal strikePrice, DateTime expirationDate, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.Option, optionRight, strikePrice, expirationDate);

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            TestHelpers.ValidateHistoricalBaseData(history, resolution, tickType, startDate, endDate, symbol);
        }

        [TestCase("AAPL", Resolution.Tick, TickType.Trade, "2024/07/02", "2024/07/30", Explicit = true, Description = "Skipped: Long execution time")]
        [TestCase("AAPL", Resolution.Tick, TickType.Quote, "2024/07/26", "2024/07/30", Explicit = true, Description = "Skipped: Long execution time")]
        [TestCase("AAPL", Resolution.Tick, TickType.Trade, "2024/07/26", "2024/07/30")]
        [TestCase("AAPL", Resolution.Second, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Second, TickType.Quote, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Minute, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Minute, TickType.Quote, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Hour, TickType.Trade, "2024/07/26", "2024/07/30")]
        [TestCase("AAPL", Resolution.Hour, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Hour, TickType.Quote, "2024/07/02", "2024/07/30")]
        [TestCase("AAPL", Resolution.Daily, TickType.Trade, "2024/07/01", "2024/07/30")]
        [TestCase("AAPL", Resolution.Daily, TickType.Quote, "2024/07/01", "2024/07/30")]
        public void GetHistoryEquityData(string ticker, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.Equity);

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            TestHelpers.ValidateHistoricalBaseData(history, resolution, tickType, startDate, endDate, symbol);
        }

        [TestCase("SPX", Resolution.Tick, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("SPX", Resolution.Second, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("SPX", Resolution.Minute, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("SPX", Resolution.Hour, TickType.Trade, "2024/07/02", "2024/07/30")]
        [TestCase("SPX", Resolution.Daily, TickType.Trade, "2024/07/01", "2024/07/30")]
        public void GetHistoryIndexData(string ticker, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.Index);

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            TestHelpers.ValidateHistoricalBaseData(history, resolution, tickType, startDate, endDate, symbol);
        }

        [TestCase("AAPL", Resolution.Tick, TickType.Trade, "2024/07/24", "2024/07/26", Explicit = true, Description = "Skipped: Long execution time")]
        public void GetHistoryTickTradeValidateOnDistinctData(string ticker, Resolution resolution, TickType tickType, DateTime startDate, DateTime endDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.Equity);

            var historyRequest = TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            var distinctHistory = history.Distinct().ToList();

            Assert.That(history.Count, Is.EqualTo(distinctHistory.Count));
        }

        [TestCase("SPY", SecurityType.Equity, Resolution.Hour, "1998/01/02", "2025/02/16", new[] { TickType.Quote, TickType.Trade })]
        [TestCase("SPY", SecurityType.Equity, Resolution.Daily, "1998/01/02", "2025/02/16", new[] { TickType.Quote, TickType.Trade })]
        [TestCase("SPY", SecurityType.Equity, Resolution.Minute, "2025/01/02", "2025/02/16", new[] { TickType.Quote, TickType.Trade })]
        [TestCase("AAPL", SecurityType.Equity, Resolution.Hour, "1998/01/02", "2025/02/16", new[] { TickType.Quote, TickType.Trade })]
        public void GetHistoryRequestWithLongRange(string ticker, SecurityType securityType, Resolution resolution, DateTime startDate, DateTime endDate, TickType[] tickTypes)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, securityType);

            var historyRequests = new List<HistoryRequest>();
            foreach (var tickType in tickTypes)
            {
                historyRequests.Add(TestHelpers.CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate));
            }

            foreach (var historyRequest in historyRequests)
            {
                var stopwatch = Stopwatch.StartNew();
                var history = _thetaDataProvider.GetHistory(historyRequest).ToList();
                stopwatch.Stop();

                Assert.IsNotEmpty(history);

                var firstDate = history.First().Time;
                var lastDate = history.Last().Time;

                Log.Trace($"[{nameof(ThetaDataHistoryProviderTests)}] Execution completed in {stopwatch.Elapsed.TotalMinutes:F2} min | " +
                          $"Symbol: {historyRequest.Symbol}, Resolution: {resolution}, TickType: {historyRequest.TickType}, Count: {history.Count}, " +
                          $"First Date: {firstDate:yyyy-MM-dd HH:mm:ss}, Last Date: {lastDate:yyyy-MM-dd HH:mm:ss}");

                // Ensure historical data is returned in chronological order
                for (var i = 1; i < history.Count; i++)
                {
                    if (history[i].Time < history[i - 1].Time)
                        Assert.Fail("Historical data is not in chronological order.");
                }
            }
        }
    }
}
