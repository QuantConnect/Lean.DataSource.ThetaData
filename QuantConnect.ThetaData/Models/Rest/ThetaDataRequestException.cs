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
/// A ThetaData request that failed. A request with no data does not throw: it returns no rows.
/// </summary>
public class ThetaDataRequestException : Exception
{
    /// <summary>
    /// The HTTP status code, or null when no response arrived.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// True when the account's plan does not include the data (HTTP 403 or ThetaData code 471).
    /// </summary>
    public bool IsEntitlementError => StatusCode is 403 or 471;

    public ThetaDataRequestException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
