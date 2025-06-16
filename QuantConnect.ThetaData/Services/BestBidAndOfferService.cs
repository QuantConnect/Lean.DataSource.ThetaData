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
using QuantConnect.Lean.DataSource.ThetaData.Models;

namespace QuantConnect.Lean.DataSource.ThetaData.Services;

/// <summary>
/// Tracks the best bid and ask (Level 1) market data for a specific trading symbol.
/// Raises an event when the best bid or ask is updated.
/// </summary>
public sealed class BestBidAndOfferService
{
    /// <summary>
    /// Occurs when the best bid or ask is updated.
    /// </summary>
    public event EventHandler<BestBidAskWithTimeZoneUpdatedEventArgs> BestBidAskUpdated;

    /// <summary>
    /// Gets the symbol this service is tracking.
    /// </summary>
    public Symbol Symbol { get; }

    /// <summary>
    /// Gets the time zone associated with the symbol's exchange.
    /// Used for consistent time stamping.
    /// </summary>
    public DateTimeZone SymbolDateTimeZone { get; }

    /// <summary>
    /// Gets the current best bid price.
    /// </summary>
    public decimal BestBidPrice { get; private set; }

    /// <summary>
    /// Gets the current size at the best bid price.
    /// </summary>
    public decimal BestBidSize { get; private set; }

    /// <summary>
    /// Gets the current best ask price.
    /// </summary>
    public decimal BestAskPrice { get; private set; }

    /// <summary>
    /// Gets the current size at the best ask price.
    /// </summary>
    public decimal BestAskSize { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BestBidAndOfferService"/> class.
    /// </summary>
    /// <param name="symbol">The symbol to track best bid/ask prices for.</param>
    /// <param name="bestBidAskUpdated">Optional event handler to attach to <see cref="BestBidAskUpdated"/>.</param>
    public BestBidAndOfferService(Symbol symbol, EventHandler<BestBidAskWithTimeZoneUpdatedEventArgs> bestBidAskUpdated)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        SymbolDateTimeZone = symbol.GetSymbolExchangeTimeZone();
        BestBidAskUpdated = bestBidAskUpdated;
    }

    /// <summary>
    /// Updates the best bid price and size if they have changed, and raises an update event.
    /// </summary>
    /// <param name="price">The new best bid price.</param>
    /// <param name="size">The new best bid size.</param>
    public void UpdateBid(decimal price, decimal size)
    {
        if (price != BestBidPrice || size != BestBidSize)
        {
            BestBidPrice = price;
            BestBidSize = size;
            BestBidAskUpdated?.Invoke(this, new BestBidAskWithTimeZoneUpdatedEventArgs(Symbol, BestBidPrice, BestBidSize, BestAskPrice, BestAskSize, SymbolDateTimeZone));
        }
    }

    /// <summary>
    /// Updates the best ask price and size if they have changed, and raises an update event.
    /// </summary>
    /// <param name="price">The new best ask price.</param>
    /// <param name="size">The new best ask size.</param>
    public void UpdateAsk(decimal price, decimal size)
    {
        if (price != BestAskPrice || size != BestAskSize)
        {
            BestAskPrice = price;
            BestAskSize = size;
            BestBidAskUpdated?.Invoke(this, new BestBidAskWithTimeZoneUpdatedEventArgs(Symbol, BestBidPrice, BestBidSize, BestAskPrice, BestAskSize, SymbolDateTimeZone));
        }
    }
}


