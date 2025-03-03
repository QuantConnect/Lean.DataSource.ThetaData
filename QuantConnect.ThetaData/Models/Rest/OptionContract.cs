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
/// Represents a contract in a list of options contracts.
/// </summary>
[JsonConverter(typeof(OptionContractConverter))]
public class OptionContract
{
    /// <summary>
    /// Gets the root symbol of the contract.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Gets the expiration date of the contract.
    /// </summary>
    public DateTime Expiration { get; }

    /// <summary>
    /// Gets the strike price of the contract.
    /// </summary>
    /// <remarks>
    /// The strike price (scaled by 1000, e.g., 260000 represents 260.00).
    /// </remarks>
    public decimal Strike { get; }

    /// <summary>
    /// Gets the option right type, such as "Call" or "Put".
    /// </summary>
    public string Right { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionContract"/> struct.
    /// </summary>
    /// <param name="root">The root symbol of the contract.</param>
    /// <param name="expiration">The expiration date of the contract.</param>
    /// <param name="strike">The strike price of the contract.</param>
    /// <param name="right">The option right type (e.g., "Call" or "Put").</param>
    public OptionContract(string root, DateTime expiration, decimal strike, string right)
    {
        Root = root;
        Expiration = expiration;
        Strike = strike;
        Right = right;
    }
}
