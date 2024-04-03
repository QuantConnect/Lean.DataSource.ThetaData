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
using QuantConnect.Util;
using QuantConnect.Tests;
using QuantConnect.Securities;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    public class ThetaDataOptionChainProviderTests
    {
        private ThetaDataOptionChainProvider _thetaDataOptionChainProvider;

        [SetUp]
        public void SetUp()
        {
            _thetaDataOptionChainProvider = new(new ThetaDataSymbolMapper(), new ThetaDataRestApiClient());
        }

        private static IEnumerable<Symbol> UnderlyingSymbols
        {
            get
            {
                TestGlobals.Initialize();
                yield return Symbol.Create("XEO", SecurityType.Index, Market.USA);
                yield return Symbol.Create("DJX", SecurityType.Index, Market.USA);
            }
        }

        [TestCaseSource(nameof(UnderlyingSymbols))]
        public void GetOptionContractList(Symbol symbol)
        {
            var referenceDate = new DateTime(2024, 03, 28);
            var optionChain = _thetaDataOptionChainProvider.GetOptionContractList(symbol, referenceDate).ToList();

            Assert.That(optionChain, Is.Not.Null.And.Not.Empty);

            // Multiple strikes
            var strikes = optionChain.Select(x => x.ID.StrikePrice).Distinct().ToList();
            Assert.That(strikes, Has.Count.GreaterThan(1).And.All.GreaterThan(0));

            // Multiple expirations
            var expirations = optionChain.Select(x => x.ID.Date).Distinct().ToList();
            Assert.That(expirations, Has.Count.GreaterThan(1));

            // All contracts have the same underlying
            var underlying = symbol.Underlying ?? symbol;
            Assert.That(optionChain.Select(x => x.Underlying), Is.All.EqualTo(underlying));
        }

        [TestCase(Futures.Indices.SP500EMini, OptionRight.Call, 100, "2024/06/21")]
        public void GetFutureOptionContractListShouldReturnNothing(string ticker, OptionRight optionRight, decimal strikePrice, DateTime expiryDate)
        {
            var symbol = TestHelpers.CreateSymbol(ticker, SecurityType.FutureOption, optionRight, strikePrice, expiryDate);

            var optionChain = _thetaDataOptionChainProvider.GetOptionContractList(symbol, expiryDate).ToList();

            Assert.IsEmpty(optionChain);
        }
    }
}
