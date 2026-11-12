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
/// Builds the ThetaData v3 REST endpoint paths. Each path is relative to <c>{base url}/v3/</c>.
/// </summary>
/// <remarks>
/// Paths come from the v3 OpenAPI spec, https://thetadata.net/docs/openapiv3.yaml.
/// </remarks>
public static class ThetaDataEndpoints
{
    /// <summary>
    /// Lists the option contracts that quoted on a date. It needs a Value plan or higher.
    /// </summary>
    public const string OptionListContractsQuote = "option/list/contracts/quote";

    /// <summary>
    /// The open interest history of option contracts. It needs a Value plan or higher.
    /// </summary>
    public const string OptionOpenInterest = "option/history/open_interest";

    /// <summary>
    /// The end-of-day report: daily OHLC, volume, and the NBBO at report time.
    /// </summary>
    public static string EndOfDay(SecurityType securityType) => $"{GetAssetPath(securityType)}/history/eod";

    /// <summary>
    /// Intraday trade bars. For an index, the OHLC of the index price.
    /// </summary>
    public static string Ohlc(SecurityType securityType) => $"{GetAssetPath(securityType)}/history/ohlc";

    /// <summary>
    /// Quotes, as ticks or as the last NBBO of each interval. An index has no quotes.
    /// </summary>
    public static string Quote(SecurityType securityType) => securityType == SecurityType.Index
        ? throw new NotSupportedException("ThetaData has no quote history for an index.")
        : $"{GetAssetPath(securityType)}/history/quote";

    /// <summary>
    /// Trade ticks. For an index, the price ticks.
    /// </summary>
    public static string Trade(SecurityType securityType) => securityType == SecurityType.Index
        ? "index/history/price"
        : $"{GetAssetPath(securityType)}/history/trade";

    /// <summary>
    /// Converts a LEAN resolution to a v3 <c>interval</c> value.
    /// </summary>
    public static string GetInterval(Resolution resolution) => resolution switch
    {
        Resolution.Tick => "tick",
        Resolution.Second => "1s",
        Resolution.Minute => "1m",
        Resolution.Hour => "1h",
        _ => throw new NotSupportedException($"{nameof(ThetaDataEndpoints)}.{nameof(GetInterval)}: The resolution '{resolution}' has no v3 interval.")
    };

    private static string GetAssetPath(SecurityType securityType) => securityType switch
    {
        SecurityType.Equity => "stock",
        SecurityType.Option or SecurityType.IndexOption => "option",
        SecurityType.Index => "index",
        _ => throw new NotSupportedException($"{nameof(ThetaDataEndpoints)}: The security type '{securityType}' is not supported.")
    };
}
