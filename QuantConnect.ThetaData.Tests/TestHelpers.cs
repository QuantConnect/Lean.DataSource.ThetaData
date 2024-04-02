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
using QuantConnect.Data;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    public static class TestHelpers
    {
        public static HistoryRequest CreateHistoryRequest(Symbol symbol, Resolution resolution, TickType tickType, DateTime startDateTime, DateTime endDateTime,
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

        public static Symbol CreateSymbol(string ticker, SecurityType securityType, OptionRight? optionRight = null, decimal? strikePrice = null, DateTime? expirationDate = null, string market = Market.USA)
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
