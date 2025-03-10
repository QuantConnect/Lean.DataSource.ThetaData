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

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

/// <summary>
/// Represents a bulk collection of open interest data for an option contract.
/// </summary>
public class OpenInterestBulk
{
    /// <summary>
    /// Gets the collection of open interest responses.
    /// </summary>
    public IReadOnlyCollection<OpenInterestResponse> Ticks { get; }

    /// <summary>
    /// Gets the option contract associated with the open interest data.
    /// </summary>
    public OptionContract Contract { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenInterestBulk"/> class with the specified open interest responses and contract.
    /// </summary>
    /// <param name="ticks">A collection of open interest responses.</param>
    /// <param name="contract">The option contract associated with the open interest data.</param>
    public OpenInterestBulk(IReadOnlyCollection<OpenInterestResponse> ticks, OptionContract contract) => (Ticks, Contract) = (ticks, contract);

}
