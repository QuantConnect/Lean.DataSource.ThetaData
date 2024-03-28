﻿/*
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

namespace QuantConnect.Lean.DataSource.ThetaData
{
    public static class ThetaDataExtensions
    {
        /// <summary>
        /// Converts a date string from Theta data format (yyyyMMdd) to a DateTime object.
        /// </summary>
        /// <param name="date">The date string in Theta data format (e.g., "20240303" for March 3, 2024).</param>
        /// <returns>The equivalent DateTime object.</returns>
        public static DateTime ConvertFromThetaDataDateFormat(this string date) => DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);

        /// <summary>
        /// Converts a DateTime object to Theta data format (yyyyMMdd) to a string.
        /// </summary>
        /// <param name="date">The DateTime object (e.g., new DateTime(2024, 03, 03))</param>
        /// <returns>The equivalent Theta Date string date.</returns>
        public static string ConvertToThetaDataDateFormat(this DateTime date) => date.ToStringInvariant("yyyyMMdd");

        /// <summary>
        /// Represents a collection of quote conditions with their corresponding numerical codes.
        /// </summary>
        public static Dictionary<byte, string> QuoteConditions = new()
        {
            { 0, "REGULAR" },
            { 1, "BID_ASK_AUTO_EXEC" },
            { 2, "ROTATION" },
            { 3, "SPECIALIST_ASK" },
            { 4, "SPECIALIST_BID" },
            { 5, "LOCKED" },
            { 6, "FAST_MARKET" },
            { 7, "SPECIALIST_BID_ASK" },
            { 8, "ONE_SIDE" },
            { 9, "OPENING_QUOTE" },
            { 10, "CLOSING_QUOTE" },
            { 11, "MARKET_MAKER_CLOSED" },
            { 12, "DEPTH_ON_ASK" },
            { 13, "DEPTH_ON_BID" },
            { 14, "DEPTH_ON_BID_ASK" },
            { 15, "TIER_3" },
            { 16, "CROSSED" },
            { 17, "HALTED" },
            { 18, "OPERATIONAL_HALT" },
            { 19, "NEWS_OUT" },
            { 20, "NEWS_PENDING" },
            { 21, "NON_FIRM" },
            { 22, "DUE_TO_RELATED" },
            { 23, "RESUME" },
            { 24, "NO_MARKET_MAKERS" },
            { 25, "ORDER_IMBALANCE" },
            { 26, "ORDER_INFLUX" },
            { 27, "INDICATED" },
            { 28, "PRE_OPEN" },
            { 29, "IN_VIEW_OF_COMMON" },
            { 30, "RELATED_NEWS_OUT" },
            { 32, "ADDITIONAL_INFO" },
            { 33, "RELATED_ADD_INFO" },
            { 34, "NO_OPEN_RESUME" },
            { 35, "DELETED" },
            { 36, "REGULATORY_HALT" },
            { 37, "SEC_SUSPENSION" },
            { 38, "NON_COMLIANCE" },
            { 39, "FILINGS_NOT_CURRENT" },
            { 40, "CATS_HALTED" },
            { 41, "CATS" },
            { 42, "EX_DIV_OR_SPLIT" },
            { 43, "UNASSIGNED" },
            { 44, "INSIDE_OPEN" },
            { 45, "INSIDE_CLOSED" },
            { 46, "OFFER_WANTED" },
            { 47, "BID_WANTED" },
            { 48, "CASH" },
            { 49, "INACTIVE" },
            { 50, "NATIONAL_BBO" },
            { 51, "NOMINAL" },
            { 52, "CABINET" },
            { 53, "NOMINAL_CABINET" },
            { 54, "BLANK_PRICE" },
            { 55, "SLOW_BID_ASK" },
            { 56, "SLOW_LIST" },
            { 57, "SLOW_BID" },
            { 58, "SLOW_ASK" },
            { 59, "BID_OFFER_WANTED" },
            { 60, "SUBPENNY" },
            { 61, "NON_BBO" },
            { 62, "SPECIAL_OPEN" },
            { 63, "BENCHMARK" },
            { 64, "IMPLIED" },
            { 65, "EXCHANGE_BEST" },
            { 66, "MKT_WIDE_HALT_1" },
            { 67, "MKT_WIDE_HALT_2" },
            { 68, "MKT_WIDE_HALT_3" },
            { 69, "ON_DEMAND_AUCTION" },
            { 70, "NON_FIRM_BID" },
            { 71, "NON_FIRM_ASK" },
            { 72, "RETAIL_BID" },
            { 73, "RETAIL_ASK" },
            { 74, "RETAIL_QTE" },
        };
    }
}
