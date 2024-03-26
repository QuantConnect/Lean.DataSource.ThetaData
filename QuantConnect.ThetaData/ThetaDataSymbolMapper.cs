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
    public class ThetaDataSymbolMapper : ISymbolMapper
    {
        private Dictionary<string, Symbol> _cachedSymbols = new();

        public string GetBrokerageSymbol(Symbol symbol)
        {
            switch (symbol.SecurityType)
            {
                case SecurityType.Option:
                    var brokerageTicker = GetBrokerageTicker(symbol.ID.Symbol, symbol.ID.Date, symbol.ID.StrikePrice, symbol.ID.OptionRight);
                    _cachedSymbols[brokerageTicker] = symbol;
                    return brokerageTicker;
                default:
                    throw new NotImplementedException();
            }
        }

        public Symbol GetLeanSymbolByBrokerageContract(string root, string date, decimal strike, ContractRight right)
        {
            var brokerageTicker = GetBrokerageTicker(root, date, strike, right == ContractRight.Call ? "C" : "P");

            if (_cachedSymbols.TryGetValue(brokerageTicker, out var symbol))
            {
                return symbol;
            }

            throw new NullReferenceException("");
        }

        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            return GetLeanSymbol(brokerageSymbol, securityType, market, OptionStyle.American, expirationDate, strike, optionRight);
        }

        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, OptionStyle optionStyle,
            DateTime expirationDate = new DateTime(), decimal strike = 0, OptionRight optionRight = OptionRight.Call,
            Symbol? underlying = null)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                throw new ArgumentException("Invalid symbol: " + brokerageSymbol);
            }

            var underlyingSymbolStr = underlying?.Value ?? brokerageSymbol;
            var leanSymbol = default(Symbol);
            switch (securityType)
            {
                case SecurityType.Option:
                    leanSymbol = Symbol.CreateOption(underlyingSymbolStr, market, optionStyle, optionRight, strike, expirationDate);
                    break;

                case SecurityType.IndexOption:
                    underlying ??= Symbol.Create(underlyingSymbolStr, SecurityType.Index, market);
                    leanSymbol = Symbol.CreateOption(underlying, brokerageSymbol, market, optionStyle, optionRight, strike, expirationDate);
                    break;

                case SecurityType.Equity:
                    leanSymbol = Symbol.Create(brokerageSymbol, securityType, market);
                    break;

                case SecurityType.Index:
                    leanSymbol = Symbol.Create(brokerageSymbol, securityType, market);
                    break;

                default:
                    throw new Exception($"PolygonSymbolMapper.GetLeanSymbol(): unsupported security type: {securityType}");
            }

            return leanSymbol;
        }

        private string GetBrokerageTicker(string ticker, DateTime expirationDate, decimal strikePrice, OptionRight optionRight)
        {
            return GetBrokerageTicker(
                ticker,
                expirationDate.ToStringInvariant("yyyyMMdd"),
                Math.Truncate(strikePrice * 1000m),
                optionRight == OptionRight.Call ? "C" : "P");
        }

        private string GetBrokerageTicker(string ticker, string expirationDate, decimal strikePrice, string optionRight)
        {
            return $"{ticker},{expirationDate},{strikePrice.ToStringInvariant()},{optionRight}";
        }
    }
}
