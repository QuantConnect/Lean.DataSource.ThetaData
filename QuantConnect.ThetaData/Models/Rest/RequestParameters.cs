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
/// Query parameter names of the ThetaData v3 REST API.
/// </summary>
public static class RequestParameters
{
    /// <summary>
    /// The stock, index, or option root. v2 called it "root".
    /// </summary>
    public const string Symbol = "symbol";

    /// <summary>
    /// The option expiration, YYYYMMDD, or "*" for every expiration. v2 called it "exp".
    /// </summary>
    public const string Expiration = "expiration";

    /// <summary>
    /// The option strike in dollars, for example "470.000", or "*". v2 used tenths of a cent.
    /// </summary>
    public const string Strike = "strike";

    /// <summary>
    /// "call", "put", or "both". v2 used "C" and "P".
    /// </summary>
    public const string Right = "right";

    /// <summary>
    /// The bar size, for example "1m". v2 used "ivl" in milliseconds.
    /// </summary>
    public const string Interval = "interval";

    /// <summary>
    /// The first date, YYYYMMDD, inclusive.
    /// </summary>
    public const string StartDate = "start_date";

    /// <summary>
    /// The last date, YYYYMMDD, inclusive.
    /// </summary>
    public const string EndDate = "end_date";

    /// <summary>
    /// The single date of a list request, YYYYMMDD.
    /// </summary>
    public const string Date = "date";

    /// <summary>
    /// The first time of day, HH:mm:ss, in ET. The server default is 09:30:00.
    /// </summary>
    public const string StartTime = "start_time";

    /// <summary>
    /// The last time of day, HH:mm:ss, in ET. The server default is 16:00:00.
    /// </summary>
    public const string EndTime = "end_time";

    /// <summary>
    /// The response format. The client always asks for "json".
    /// </summary>
    public const string Format = "format";
}
