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

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;

/// <summary>
/// The <c>ISubscriptionPlan</c> interface defines the base structure for different price plans offered by ThetaData for users.
/// For detailed documentation on ThetaData subscription plans, refer to the following links:
/// <see href="https://www.thetadata.net/subscribe" /> 
/// <see href="https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/1floxgrco3si8-us-options#historical-endpoint-access" />
/// </summary>
public interface ISubscriptionPlan
{
    /// <summary>
    /// Gets the set of resolutions accessible under the subscription plan.
    /// </summary>
    public HashSet<Resolution> AccessibleResolutions { get; }

    /// <summary>
    /// Gets the date when the user first accessed the subscription plan.
    /// </summary>
    public DateTime FirstAccessDate { get; }

    /// <summary>
    /// Gets the maximum number of contracts that can be streamed simultaneously under the subscription plan.
    /// </summary>
    public uint MaxStreamingContracts { get; }

    /// <summary>
    /// Represents a rate limiting mechanism that controls the rate of access to a resource.
    /// </summary>
    public RateGate? RateGate { get; }
}
