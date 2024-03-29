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
using NUnit.Framework;
using QuantConnect.Brokerages;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    public class ThetaDataSymbolMapperTests
    {
        private ISymbolMapper _symbolMapper = new ThetaDataSymbolMapper();

        [TestCase("JD", SecurityType.Equity, null, null, null, "JD", null, null, null)]
        [TestCase("SPEUFP2I", SecurityType.Index, null, null, null, "SPEUFP2I", null, null, null)]
        [TestCase("AAPL", SecurityType.Option, OptionRight.Call, 22, "2024/03/08", "AAPL", "C", "22000", "20240308")]
        [TestCase("9QE", SecurityType.Option, OptionRight.Put, 100, "2026/02/02", "9QE", "P", "100000", "20260202")]
        [TestCase("9QE", SecurityType.Option, OptionRight.Put, 123, "2026/02/02", "9QE", "P", "123000", "20260202")]
        [TestCase("SPXW", SecurityType.IndexOption, OptionRight.Call, 6700, "2022/09/30", "SPXW", "C", "6700000", "20220930")]
        [TestCase("NDX", SecurityType.IndexOption, OptionRight.Call, 1650, "2013/01/19", "NDX", "C", "1650000", "20130119")]
        public void GetDataProviderOptionTicker(string ticker, SecurityType securityType, OptionRight? optionRight, decimal? strikePrice, DateTime? expirationDate,
            string expectedTicker, string expectedOptionRight, string expectedStrike, string expectedExpirationDate)
        {
            var leanSymbol = CreateSymbol(ticker, securityType, optionRight, strikePrice, expirationDate);

            var dataProviderContract = _symbolMapper.GetBrokerageSymbol(leanSymbol).Split(',');

            Assert.That(dataProviderContract[0], Is.EqualTo(expectedTicker));

            if (securityType.IsOption())
            {
                Assert.That(dataProviderContract[1], Is.EqualTo(expectedExpirationDate));
                Assert.That(dataProviderContract[2], Is.EqualTo(expectedStrike));
                Assert.That(dataProviderContract[3], Is.EqualTo(expectedOptionRight));
            }
        }

        [TestCase("AAPL", ContractSecurityType.Option, "C", 22000, "20240308", Market.CBOE, OptionRight.Call, 22, "2024/03/08")]
        [TestCase("AAPL", ContractSecurityType.Option, "P", 1000000, "20240303", Market.CBOE, OptionRight.Put, 1000, "2024/03/03")]
        [TestCase("AAPL", ContractSecurityType.Equity, "", 0, "", Market.CBOE, null, null, null)]
        [TestCase("INTL", ContractSecurityType.Equity, "", 0, "", Market.CBOE, null, null, null)]
        public void GetLeanSymbol(
            string dataProviderTicker,
            ContractSecurityType dataProviderContractSecurityType,
            string dataProviderOptionRight,
            decimal dataProviderStrike,
            string dataProviderExpirationDate,
            string expectedMarket,
            OptionRight? expectedOptionRight = null,
            decimal? expectedStrikePrice = null,
            DateTime? expectedExpiryDateTime = null)
        {
            var leanSymbol = (_symbolMapper as ThetaDataSymbolMapper)
                .GetLeanSymbol(dataProviderTicker, dataProviderContractSecurityType, dataProviderExpirationDate, dataProviderStrike, dataProviderOptionRight);

            var expectedLeanSymbol =
                CreateSymbol(dataProviderContractSecurityType, dataProviderTicker, expectedOptionRight, expectedStrikePrice, expectedExpiryDateTime, expectedMarket);

            Assert.That(leanSymbol, Is.EqualTo(expectedLeanSymbol));
        }

        private Symbol CreateSymbol(ContractSecurityType contractSecurityType, string ticker, OptionRight? optionRight, decimal? strikePrice, DateTime? expirationDate, string market = Market.USA)
        {
            switch (contractSecurityType)
            {
                case ContractSecurityType.Option:
                    return CreateSymbol(ticker, SecurityType.Option, optionRight, strikePrice, expirationDate, market);
                case ContractSecurityType.Equity:
                    return CreateSymbol(ticker, SecurityType.Equity, market: market);
                default:
                    throw new NotSupportedException($"The contract security type '{contractSecurityType}' is not supported.");
            }
        }

        private Symbol CreateSymbol(string ticker, SecurityType securityType, OptionRight? optionRight = null, decimal? strikePrice = null, DateTime? expirationDate = null, string market = Market.CBOE)
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
