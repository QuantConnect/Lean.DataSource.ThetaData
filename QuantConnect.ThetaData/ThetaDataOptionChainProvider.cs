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

using RestSharp;
using QuantConnect.Logging;
using QuantConnect.Interfaces;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;
using QuantConnect.Lean.DataSource.ThetaData.Models.Common;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// ThetaData.net implementation of <see cref="IOptionChainProvider"/>
    /// </summary>
    public class ThetaDataOptionChainProvider : IOptionChainProvider
    {
        /// <summary>
        /// Provides the TheData mapping between Lean symbols and brokerage specific symbols.
        /// </summary>
        private readonly ThetaDataSymbolMapper _symbolMapper;

        /// <summary>
        /// Provider The TheData Rest Api client instance.
        /// </summary>
        private readonly ThetaDataRestApiClient _restApiClient;

        /// <summary>
        /// Collection of pre-defined option rights.
        /// Initialized for performance optimization as the API only returns strike price without indicating the right.
        /// </summary>
        private readonly IEnumerable<OptionRight> optionRights = new[] { OptionRight.Call, OptionRight.Put };

        /// <summary>
        /// Indicates whether the warning for invalid <see cref="SecurityType"/> has been fired.
        /// </summary>
        private volatile bool _unsupportedSecurityTypeWarningFired;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataOptionChainProvider"/>
        /// </summary>
        /// <param name="symbolMapper">The TheData mapping between Lean symbols and brokerage specific symbols.</param>
        /// <param name="restApiClient">The client for interacting with the Theta Data REST API by sending HTTP requests</param>
        public ThetaDataOptionChainProvider(ThetaDataSymbolMapper symbolMapper, ThetaDataRestApiClient restApiClient)
        {
            _symbolMapper = symbolMapper;
            _restApiClient = restApiClient;
        }

        /// <inheritdoc />
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            if ((symbol.SecurityType.IsOption() && symbol.SecurityType == SecurityType.FutureOption) ||
                (symbol.HasUnderlying && symbol.Underlying.SecurityType != SecurityType.Equity && symbol.Underlying.SecurityType != SecurityType.Index))
            {
                if (!_unsupportedSecurityTypeWarningFired)
                {
                    _unsupportedSecurityTypeWarningFired = true;
                    Log.Trace($"{nameof(ThetaDataOptionChainProvider)}.{nameof(GetOptionContractList)}: Unsupported security type {symbol.SecurityType}");
                }
                yield break;
            }

            var underlying = symbol.SecurityType.IsOption() ? symbol.Underlying : symbol;
            var optionsSecurityType = underlying.SecurityType == SecurityType.Index ? SecurityType.IndexOption : SecurityType.Option;
            var optionStyle = optionsSecurityType.DefaultOptionStyle();

            // just using quote, which is the most inclusive
            var request = new RestRequest($"/list/contracts/option/quote", Method.GET);

            request.AddQueryParameter(RequestParameters.StartDate, date.ConvertToThetaDataDateFormat());
            request.AddQueryParameter("root", underlying.Value);

            foreach (var option in _restApiClient.ExecuteRequest<BaseResponse<QuoteListContract>>(request).SelectMany(x => x.Response))
            {
                yield return _symbolMapper.GetLeanSymbol(underlying.Value, optionsSecurityType, underlying.ID.Market, optionStyle,
                    option.Expiry, option.Strike, option.Right == "C" ? OptionRight.Call : OptionRight.Put, underlying);
            }
        }
    }
}
