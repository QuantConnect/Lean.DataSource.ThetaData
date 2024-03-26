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

using System.Globalization;
using QuantConnect.Lean.DataSource.ThetaData.Models.Enums;
using QuantConnect.Lean.DataSource.ThetaData.Models.WebSocket;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    public static class ThetaDataExtensions
    {
        /// <summary>
        /// Converts a date string in Theta data format (yyyyMMdd) to a DateTime object.
        /// </summary>
        /// <param name="date">The date string in Theta data format (e.g., "20240303" for March 3, 2024).</param>
        /// <returns>The equivalent DateTime object.</returns>
        public static DateTime ConvertFromThetaDataFormat(this string date) => DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
    }
}
