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

using QuantConnect.Brokerages;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Index Option Tickers: https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/s1ezbyfni6rw0-index-option-tickers
    /// </summary>
    public class ThetaDataSymbolMapper : ISymbolMapper
    {
        private Dictionary<string, Symbol> _dataProviderSymbolCache = new();
        private Dictionary<Symbol, string> _leanSymbolCache = new();

        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (!_leanSymbolCache.TryGetValue(symbol, out var dataProviderTicker))
            {
                switch (symbol.SecurityType)
                {
                    case SecurityType.Equity:
                    case SecurityType.Index:
                        dataProviderTicker = symbol.Value;
                        break;
                    case SecurityType.Option:
                    case SecurityType.IndexOption:
                        dataProviderTicker = GetDataProviderOptionTickerByLeanSymbol(symbol);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                _dataProviderSymbolCache[dataProviderTicker] = symbol;
                _leanSymbolCache[symbol] = dataProviderTicker;
            }

            return dataProviderTicker;
        }

        public Symbol GetOptionLeanSymbol(string root, ContractSecurityType contractSecurityType, string dataProviderDate, decimal strike, string right)
        {
            var dataProviderTicker = GetDataProviderOptionTicker(root, dataProviderDate, strike, right);

            if (_dataProviderSymbolCache.TryGetValue(dataProviderTicker, out var symbol))
            {
                return symbol;
            }

            return GetLeanSymbol(
                root,
                ConvertContractSecurityTypeFromThetaDataFormat(contractSecurityType),
                Market.CBOE, // docs: https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/1872cab32381d-the-si-ps#options-opra
                dataProviderDate.ConvertFromThetaDataDateFormat(),
                strike,
                ConvertContractOptionRightFromThetaDataFormat(right));
        }

        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            return GetLeanSymbol(brokerageSymbol, securityType, market, OptionStyle.American, expirationDate, strike, optionRight);
        }

        public Symbol GetLeanSymbol(string dataProviderTicker, SecurityType securityType, string market, OptionStyle optionStyle,
            DateTime expirationDate = new DateTime(), decimal strike = 0, OptionRight optionRight = OptionRight.Call,
            Symbol? underlying = null)
        {
            if (string.IsNullOrWhiteSpace(dataProviderTicker))
            {
                throw new ArgumentException("Invalid symbol: " + dataProviderTicker);
            }

            var underlyingSymbolStr = underlying?.Value ?? dataProviderTicker;
            var leanSymbol = default(Symbol);

            if (strike != 0m)
            {
                strike = ConvertStrikePriceFromThetaDataFormat(strike);
            }

            switch (securityType)
            {
                case SecurityType.Option:
                    leanSymbol = Symbol.CreateOption(underlyingSymbolStr, market, optionStyle, optionRight, strike, expirationDate);
                    dataProviderTicker = GetDataProviderOptionTickerByLeanSymbol(leanSymbol);
                    break;

                case SecurityType.IndexOption:
                    underlying ??= Symbol.Create(underlyingSymbolStr, SecurityType.Index, market);
                    leanSymbol = Symbol.CreateOption(underlying, dataProviderTicker, market, optionStyle, optionRight, strike, expirationDate);
                    dataProviderTicker = GetDataProviderOptionTickerByLeanSymbol(leanSymbol);
                    break;

                case SecurityType.Equity:
                    leanSymbol = Symbol.Create(dataProviderTicker, securityType, market);
                    break;

                case SecurityType.Index:
                    leanSymbol = Symbol.Create(dataProviderTicker, securityType, market);
                    break;

                default:
                    throw new Exception($"PolygonSymbolMapper.GetLeanSymbol(): unsupported security type: {securityType}");
            }

            return leanSymbol;
        }

        private string GetDataProviderOptionTickerByLeanSymbol(Symbol symbol)
        {
            return GetDataProviderOptionTicker(
                            symbol.ID.Symbol,
                            symbol.ID.Date.ConvertToThetaDataDateFormat(),
                            ConvertStrikePriceToThetaDataFormat(symbol.ID.StrikePrice),
                            symbol.ID.OptionRight == OptionRight.Call ? "C" : "P");
        }

        private string GetDataProviderOptionTicker(string ticker, string expirationDate, decimal strikePrice, string optionRight)
        {
            return GetDataProviderOptionTicker(ticker, expirationDate, strikePrice.ToStringInvariant(), optionRight);
        }

        private string GetDataProviderOptionTicker(string ticker, string expirationDate, string strikePrice, string optionRight)
        {
            return $"{ticker},{expirationDate},{strikePrice},{optionRight}";
        }

        private SecurityType ConvertContractSecurityTypeFromThetaDataFormat(ContractSecurityType contractSecurityType) => contractSecurityType switch
        {
            ContractSecurityType.Option => SecurityType.Option,
            _ => throw new NotSupportedException($"The Contract Security Type '{contractSecurityType}' is not supported.")
        };

        private OptionRight ConvertContractOptionRightFromThetaDataFormat(string contractOptionRight) 
            => contractOptionRight == "C" ? OptionRight.Call : OptionRight.Put;

        private string ConvertStrikePriceToThetaDataFormat(decimal value) => Math.Truncate(value * 1000m).ToStringInvariant();

        private decimal ConvertStrikePriceFromThetaDataFormat(decimal value) => value / 1000m;
    }
}
