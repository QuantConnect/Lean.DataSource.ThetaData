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

using Newtonsoft.Json;
using QuantConnect.Lean.DataSource.ThetaData.Converters;

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

/// <summary>
/// Represents a response containing open interest data.
/// </summary>
[JsonConverter(typeof(ThetaDataOpenInterestConverter))]
public readonly struct OpenInterestResponse
{
    /// <summary>
    /// Gets the time at which open interest was reported, represented in milliseconds since 00:00:00.000 (midnight) Eastern Time (ET).
    /// </summary>
    public uint TimeMilliseconds { get; }

    /// <summary>
    /// Gets the total amount of outstanding contracts.
    /// </summary>
    public decimal OpenInterest { get; }

    /// <summary>
    /// Gets the date of the open interest data in the format YYYYMMDD. For example, "20240328" represents March 28, 2024.
    /// </summary>
    public string Date { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenInterestResponse"/> struct with the specified time, open interest, and date.
    /// </summary>
    /// <param name="timeMilliseconds">The time in milliseconds since midnight Eastern Time (ET).</param>
    /// <param name="openInterest">The total amount of outstanding contracts.</param>
    /// <param name="date">The date of the data in the format YYYYMMDD.</param>
    public OpenInterestResponse(uint timeMilliseconds, decimal openInterest, string date)
    {
        TimeMilliseconds = timeMilliseconds;
        OpenInterest = openInterest;
        Date = date;
    }
}
