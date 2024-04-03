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

using QuantConnect.Interfaces;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IDataQueueUniverseProvider"/>
    /// </summary>
    public partial class ThetaDataProvider : IDataQueueUniverseProvider
    {
        private IOptionChainProvider _optionChainProvider;

        /// <inheritdoc />
        public bool CanPerformSelection()
        {
            return IsConnected;
        }

        /// <inheritdoc />
        public IEnumerable<Symbol> LookupSymbols(Symbol symbol, bool includeExpired, string? securityCurrency = null)
        {
            var utcNow = TimeProvider.GetUtcNow();
            var symbols = GetOptionChain(symbol, utcNow.Date);

            foreach (var optionSymbol in symbols)
            {
                yield return optionSymbol;
            }
        }

        /// <summary>
        /// Retrieves a collection of option contracts for a given security symbol and requested date.
        /// We have returned option contracts from <paramref name="requestedDate"/> to a future date, excluding expired contracts.
        /// </summary>
        /// <param name="symbol">The unique security identifier for which option contracts are to be retrieved.</param>
        /// <param name="requestedDate">The date from which to find option contracts.</param>
        /// <returns>A collection of option contracts.</returns>
        /// <exception cref="ArgumentException">Thrown when the security type is not supported or invalid.</exception>
        public IEnumerable<Symbol> GetOptionChain(Symbol symbol, DateTime requestedDate)
        {
            if ((symbol.SecurityType.IsOption() && symbol.SecurityType == SecurityType.FutureOption) ||
                (symbol.HasUnderlying && symbol.Underlying.SecurityType != SecurityType.Equity && symbol.Underlying.SecurityType != SecurityType.Index))
            {
                throw new ArgumentException($"Unsupported security type {symbol.SecurityType}");
            }

            return _optionChainProvider.GetOptionContractList(symbol, requestedDate);
        }

        public IEnumerable<Symbol> GetOptionChain(Symbol requestedSymbol, DateTime startDate, DateTime endDate)
        {
            foreach (var symbol in _optionChainProvider.GetOptionContractList(requestedSymbol, startDate))
            {
                if (symbol.ID.Date >= startDate && symbol.ID.Date <= endDate)
                {
                    yield return symbol;
                }
            }
        }
    }
}
