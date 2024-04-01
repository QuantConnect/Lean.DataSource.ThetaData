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
using QuantConnect.Securities;
using QuantConnect.Algorithm.CSharp;

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

            var historyRequest = CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest)?.ToList();

            Assert.IsNull(history);
        }

        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Daily, TickType.Trade)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Daily, TickType.OpenInterest)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Tick, TickType.Quote)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Second, TickType.Quote)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Hour, TickType.Quote)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 170, "2024/03/28", Resolution.Daily, TickType.Quote)]
        public void GetHistoryRequest(string ticker, SecurityType securityType, OptionRight optionRight, decimal strikePrice, DateTime expirationDate, Resolution resolution, TickType tickType)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, securityType, optionRight, strikePrice, expirationDate);

            var startDate = new DateTime(2024, 3, 18);
            var endDate = new DateTime(2024, 3, 28);

            var historyRequest = CreateHistoryRequest(symbol, resolution, tickType, startDate, endDate);

            var history = _thetaDataProvider.GetHistory(historyRequest).ToList();

            Assert.IsNotEmpty(history);
        }

        internal static HistoryRequest CreateHistoryRequest(Symbol symbol, Resolution resolution, TickType tickType, DateTime startDateTime, DateTime endDateTime,
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
    }
}
