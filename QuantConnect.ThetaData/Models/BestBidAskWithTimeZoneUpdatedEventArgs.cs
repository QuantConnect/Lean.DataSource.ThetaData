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

using NodaTime;

namespace QuantConnect.Lean.DataSource.ThetaData.Models;

/// <summary>
/// Event arguments class for best bid/ask updates with time zone information.
/// </summary>
public sealed class BestBidAskWithTimeZoneUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the symbol associated with the update.
    /// </summary>
    public Symbol Symbol { get; }

    /// <summary>
    /// Gets the newly updated best bid price.
    /// </summary>
    public decimal BestBidPrice { get; }

    /// <summary>
    /// Gets the newly updated best bid size.
    /// </summary>
    public decimal BestBidSize { get; }

    /// <summary>
    /// Gets the newly updated best ask price.
    /// </summary>
    public decimal BestAskPrice { get; }

    /// <summary>
    /// Gets the newly updated best ask size.
    /// </summary>
    public decimal BestAskSize { get; }

    /// <summary>
    /// Gets the time zone associated with the symbol.
    /// </summary>
    public DateTimeZone SymbolDateTimeZone { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BestBidAskWithTimeZoneUpdatedEventArgs"/> class.
    /// </summary>
    /// <param name="symbol">The trading symbol.</param>
    /// <param name="bestBidPrice">The newly updated best bid price.</param>
    /// <param name="bestBidSize">The newly updated best bid size.</param>
    /// <param name="bestAskPrice">The newly updated best ask price.</param>
    /// <param name="bestAskSize">The newly updated best ask size.</param>
    /// <param name="symbolDateTimeZone">The time zone associated with the symbol.</param>
    public BestBidAskWithTimeZoneUpdatedEventArgs(
        Symbol symbol,
        decimal bestBidPrice,
        decimal bestBidSize,
        decimal bestAskPrice,
        decimal bestAskSize,
        DateTimeZone symbolDateTimeZone)
    {
        Symbol = symbol;
        BestBidPrice = bestBidPrice;
        BestBidSize = bestBidSize;
        BestAskPrice = bestAskPrice;
        BestAskSize = bestAskSize;
        SymbolDateTimeZone = symbolDateTimeZone;
    }
}
