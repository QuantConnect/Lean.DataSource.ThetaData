﻿/*
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

using Newtonsoft.Json;
using QuantConnect.Lean.DataSource.ThetaData.Converters;

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

/// <summary>
/// Represents a Trade response containing information about the last NBBO Trade for a financial instrument.
/// </summary>
[JsonConverter(typeof(ThetaDataTradeConverter))]
public readonly struct TradeResponse
{
    /// <summary>
    /// The milliseconds since 00:00:00.000 (midnight) Eastern Time (ET).
    /// </summary>
    public uint TimeMilliseconds { get; }

    /// <summary>
    /// The trade condition.
    /// </summary>
    public string Condition { get; }

    /// <summary>
    /// The amount of contracts traded.
    /// </summary>
    public decimal Size { get; }

    /// <summary>
    /// The exchange the trade was executed.
    /// </summary>
    public byte Exchange { get; }

    /// <summary>
    /// The price of the trade.
    /// </summary>
    public decimal Price { get; }

    /// <summary>
    /// The date formatted as YYYYMMDD. (e.g. "20240328" -> 2024/03/28)
    /// </summary>
    public DateTime Date { get; }

    /// <summary>
    /// Gets the DateTime representation of the last trade time. DateTime is New York Time (EST) Time Zone!
    /// </summary>
    /// <remarks>
    /// This property calculates the <see cref="Date"/> by adding the <seealso cref="TimeMilliseconds"/> to the Date property.
    /// </remarks>
    public DateTime DateTimeMilliseconds { get => Date.AddMilliseconds(TimeMilliseconds); }

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeResponse"/> struct with the specified parameters.
    /// </summary>
    /// <param name="timeMilliseconds">The milliseconds since midnight ET.</param>
    /// <param name="condition">The trade condition.</param>
    /// <param name="size">The amount of contracts traded.</param>
    /// <param name="exchange">The exchange where the trade was executed.</param>
    /// <param name="price">The price of the trade.</param>
    /// <param name="date">The date formatted as YYYYMMDD.</param>
    public TradeResponse(uint timeMilliseconds, string condition, decimal size, byte exchange, decimal price, DateTime date)
    {
        TimeMilliseconds = timeMilliseconds;
        Condition = condition;
        Size = size;
        Exchange = exchange;
        Price = price;
        Date = date;
    }
}
