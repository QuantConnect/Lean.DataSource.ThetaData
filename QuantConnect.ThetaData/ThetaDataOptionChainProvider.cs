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

using QuantConnect.Logging;
using QuantConnect.Interfaces;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IOptionChainProvider"/>
    /// </summary>
    /// <remarks>
    /// It lists the contracts that quoted on the date (<c>/v3/option/list/contracts/quote</c>, Value plan or higher).
    /// When the plan denies that request, it lists the contracts in the end-of-day report of the date instead,
    /// which the Free plan allows. The report exists after about 17:15 ET, so on the Free plan the chain of today is empty until then.
    /// </remarks>
    public class ThetaDataOptionChainProvider : IOptionChainProvider
    {
        private readonly ThetaDataSymbolMapper _symbolMapper;
        private readonly ThetaDataRestApiClient _restApiClient;
        private readonly ThetaDataOptionEndOfDayLoader _endOfDayLoader;

        /// <summary>
        /// Indicates whether the warning for invalid <see cref="SecurityType"/> has been fired.
        /// </summary>
        private volatile bool _unsupportedSecurityTypeWarningFired;

        /// <summary>
        /// True after the plan denied the contract list request. Later calls go to the end-of-day report directly.
        /// </summary>
        private volatile bool _useEndOfDayReport;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataOptionChainProvider"/>
        /// </summary>
        /// <param name="symbolMapper">The TheData mapping between Lean symbols and brokerage specific symbols.</param>
        /// <param name="restApiClient">The client for interacting with the Theta Data REST API by sending HTTP requests</param>
        /// <param name="endOfDayLoader">Loads the end-of-day report of a chain. Null creates one on <paramref name="restApiClient"/>.</param>
        public ThetaDataOptionChainProvider(ThetaDataSymbolMapper symbolMapper, ThetaDataRestApiClient restApiClient, ThetaDataOptionEndOfDayLoader? endOfDayLoader = null)
        {
            _symbolMapper = symbolMapper;
            _restApiClient = restApiClient;
            _endOfDayLoader = endOfDayLoader ?? new ThetaDataOptionEndOfDayLoader(restApiClient);
        }

        /// <inheritdoc />
        /// <param name="symbol">The underlying (SPY, SPX) or a canonical option (?SPY, ?SPXW). For an index, the canonical option selects the root, for example SPXW instead of SPX.</param>
        /// <param name="date">The trading day.</param>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            if (symbol.SecurityType == SecurityType.FutureOption ||
                (symbol.SecurityType.IsOption() && !symbol.HasUnderlying) ||
                (symbol.HasUnderlying && symbol.Underlying.SecurityType != SecurityType.Equity && symbol.Underlying.SecurityType != SecurityType.Index) ||
                (!symbol.SecurityType.IsOption() && symbol.SecurityType != SecurityType.Equity && symbol.SecurityType != SecurityType.Index))
            {
                if (!_unsupportedSecurityTypeWarningFired)
                {
                    _unsupportedSecurityTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataOptionChainProvider)}.{nameof(GetOptionContractList)}: Unsupported security type {symbol.SecurityType}");
                }
                return Enumerable.Empty<Symbol>();
            }

            // For ?SPXW the root is SPXW. For SPX or ?SPX it is SPX.
            var root = symbol.SecurityType.IsOption() ? symbol.ID.Symbol : symbol.Value;
            return GetContracts(root, date.Date)
                .Select(contract => _symbolMapper.GetLeanOptionSymbol(contract, symbol))
                .Distinct()
                .ToList();
        }

        private IEnumerable<ThetaDataContract> GetContracts(string root, DateTime date)
        {
            if (!_useEndOfDayReport)
            {
                try
                {
                    return _restApiClient.GetRows<ContractListRow>(ThetaDataEndpoints.OptionListContractsQuote, new Dictionary<string, string>
                    {
                        [RequestParameters.Symbol] = root,
                        [RequestParameters.Date] = date.ConvertToThetaDataDateFormat()
                    }).Select(row => row.ToContract()).ToList();
                }
                catch (ThetaDataRequestException e) when (e.IsEntitlementError)
                {
                    _useEndOfDayReport = true;
                    Log.Trace($"{nameof(ThetaDataOptionChainProvider)}: The plan does not include the contract list, so the chain comes from the end-of-day report. {e.Message}");
                }
            }
            return _endOfDayLoader.GetRows(root, date).Select(row => row.Contract!).ToList();
        }
    }
}
