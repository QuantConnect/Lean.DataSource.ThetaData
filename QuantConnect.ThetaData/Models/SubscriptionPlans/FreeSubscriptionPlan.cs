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

using QuantConnect.Util;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;

namespace QuantConnect.Lean.DataSource.ThetaData.Models.SubscriptionPlans;

public class FreeSubscriptionPlan : ISubscriptionPlan
{
    public HashSet<Resolution> AccessibleResolutions => new() { Resolution.Daily };

    public DateTime FirstAccessDate => new DateTime(2023, 06, 01);

    public uint MaxStreamingContracts => 0;

    /// <summary>
    /// The ThetaData docs give 2 limits for free accounts: "20-requests/minute" in the text and "30 reqs/min" in a table.
    /// The lower one is used. One instance per plan, so every caller shares the same limit.
    /// </summary>
    public RateGate RateGate { get; } = new(20, TimeSpan.FromMinutes(1));

    /// <inheritdoc />
    public int MaxConcurrentRequests => 1;
}
