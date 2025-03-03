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
/// Represents a bulk collection of end-of-day reports for an option contract.
/// </summary>
public class EndOfDayBulk
{
    /// <summary>
    /// Gets the collection of end-of-day report responses.
    /// </summary>
    public IReadOnlyCollection<EndOfDayReportResponse> Ticks { get; }

    /// <summary>
    /// Gets the option contract associated with the end-of-day reports.
    /// </summary>
    public OptionContract Contract { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EndOfDayBulk"/> class with the specified report responses and contract.
    /// </summary>
    /// <param name="ticks">A collection of end-of-day report responses.</param>
    /// <param name="contract">The option contract associated with the reports.</param>
    public EndOfDayBulk(IReadOnlyCollection<EndOfDayReportResponse> ticks, OptionContract contract)
    {
        Ticks = ticks;
        Contract = contract;
    }
}
