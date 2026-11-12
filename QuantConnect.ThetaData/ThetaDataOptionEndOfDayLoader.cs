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

using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Loads the end-of-day rows of every option contract of a root on 1 day, in 1 request
    /// (<c>/v3/option/history/eod</c> with <c>expiration=*</c>). The Free plan allows this request.
    /// </summary>
    /// <remarks>
    /// For SPY on 2026-10-20 the request returned 5 MB in about 20 seconds.
    /// LEAN asks for the quote and trade data of the same day one after the other, so the loader keeps the last day it loaded.
    /// </remarks>
    public class ThetaDataOptionEndOfDayLoader
    {
        private readonly ThetaDataRestApiClient _restApiClient;
        private readonly object _cacheLock = new();
        private (string Root, DateTime Date, Lazy<IReadOnlyList<EndOfDayRow>> Rows)? _lastDay;

        public ThetaDataOptionEndOfDayLoader(ThetaDataRestApiClient restApiClient)
        {
            _restApiClient = restApiClient;
        }

        /// <summary>
        /// Gets the end-of-day row of each contract of <paramref name="root"/> on <paramref name="date"/>.
        /// </summary>
        /// <param name="root">The option root, for example "SPY" or "SPXW".</param>
        /// <param name="date">The trading day.</param>
        /// <returns>The rows, or no rows when ThetaData has no data for that day.</returns>
        public IReadOnlyList<EndOfDayRow> GetRows(string root, DateTime date)
        {
            Lazy<IReadOnlyList<EndOfDayRow>> rows;
            lock (_cacheLock)
            {
                if (_lastDay is not { } last || last.Root != root || last.Date != date.Date)
                {
                    var day = date.Date.ConvertToThetaDataDateFormat();
                    var parameters = new Dictionary<string, string>
                    {
                        [RequestParameters.Symbol] = root,
                        [RequestParameters.Expiration] = "*",
                        [RequestParameters.StartDate] = day,
                        [RequestParameters.EndDate] = day
                    };
                    _lastDay = (root, date.Date, new Lazy<IReadOnlyList<EndOfDayRow>>(
                        () => _restApiClient.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Option), parameters)));
                }
                rows = _lastDay.Value.Rows;
            }
            try
            {
                return rows.Value;
            }
            catch
            {
                // Lazy keeps the exception, so drop the entry: the next call for this day tries again.
                lock (_cacheLock)
                {
                    if (_lastDay is { } last && ReferenceEquals(last.Rows, rows))
                    {
                        _lastDay = null;
                    }
                }
                throw;
            }
        }
    }
}
