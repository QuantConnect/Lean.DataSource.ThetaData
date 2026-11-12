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

using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Converts v3 rows to LEAN data. Each method returns null for a row that holds no market observation.
    /// It never invents a price.
    /// </summary>
    /// <remarks>
    /// ThetaData sends times as ET wall-clock times, see https://thetadata.net/docs/Articles/Data-And-Requests/OHLC-EOD.html.
    /// </remarks>
    public static class ThetaDataBarConverter
    {
        /// <summary>
        /// The time zone of every ThetaData timestamp.
        /// </summary>
        public static readonly DateTimeZone ThetaDataTimeZone = TimeZones.NewYork;

        /// <summary>
        /// Creates a daily trade bar from an end-of-day row.
        /// </summary>
        /// <returns>
        /// Null when the row has no trades. ThetaData then sends 0 for open, high, low, and close,
        /// "there were no qualifying trades during that interval". An index row has a count and volume
        /// of 0 but real prices, so the check uses the prices, not the count.
        /// </returns>
        public static TradeBar? ToDailyTradeBar(EndOfDayRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            if (!HasValidPrices(row.Open, row.High, row.Low, row.Close))
            {
                return null;
            }
            return new TradeBar(ToTradingDay(row, exchangeTimeZone), symbol, row.Open, row.High, row.Low, row.Close, row.Volume, Time.OneDay);
        }

        /// <summary>
        /// Creates a daily quote bar from the NBBO in an end-of-day row.
        /// </summary>
        /// <remarks>
        /// The NBBO is the one at report time, about 17:15 ET, not at the 16:00 close.
        /// </remarks>
        /// <returns>Null when a side has a price or size of 0, as before v3.</returns>
        public static QuoteBar? ToDailyQuoteBar(EndOfDayRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            return CreateQuoteBar(ToTradingDay(row, exchangeTimeZone), symbol, row.Bid, row.BidSize, row.Ask, row.AskSize, Time.OneDay);
        }

        /// <summary>
        /// Creates an intraday trade bar. The row timestamp is the bar start.
        /// </summary>
        /// <returns>Null when the interval has no trades.</returns>
        public static TradeBar? ToTradeBar(OhlcRow row, Symbol symbol, TimeSpan period, DateTimeZone exchangeTimeZone)
        {
            if (!HasValidPrices(row.Open, row.High, row.Low, row.Close))
            {
                return null;
            }
            return new TradeBar(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, row.Open, row.High, row.Low, row.Close, row.Volume, period);
        }

        /// <summary>
        /// Creates an intraday quote bar from the last NBBO of the interval.
        /// </summary>
        /// <returns>Null when a side has a price or size of 0.</returns>
        public static QuoteBar? ToQuoteBar(QuoteRow row, Symbol symbol, TimeSpan period, DateTimeZone exchangeTimeZone)
        {
            return CreateQuoteBar(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, row.Bid, row.BidSize, row.Ask, row.AskSize, period);
        }

        /// <summary>
        /// Creates a quote tick.
        /// </summary>
        /// <returns>Null when a side has a price or size of 0.</returns>
        public static Tick? ToQuoteTick(QuoteRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            if (!HasBothSides(row.Bid, row.BidSize, row.Ask, row.AskSize))
            {
                return null;
            }
            return new Tick(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, string.Empty, row.AskExchange.TryGetExchangeOrDefault(),
                row.BidSize, row.Bid, row.AskSize, row.Ask);
        }

        /// <summary>
        /// Creates a trade tick.
        /// </summary>
        /// <returns>Null when the price is not positive.</returns>
        public static Tick? ToTradeTick(TradeRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            if (row.Price <= 0)
            {
                return null;
            }
            return new Tick(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, row.Condition.ToStringInvariant(), row.Exchange.TryGetExchangeOrDefault(), row.Size, row.Price);
        }

        /// <summary>
        /// Creates an index price tick.
        /// </summary>
        /// <returns>Null when the price is not positive.</returns>
        public static Tick? ToIndexTick(PriceRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            if (row.Price <= 0)
            {
                return null;
            }
            return new Tick(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, string.Empty, string.Empty, 0m, row.Price);
        }

        /// <summary>
        /// Creates an open interest point at the time ThetaData reports it.
        /// </summary>
        public static OpenInterest ToOpenInterest(OpenInterestRow row, Symbol symbol, DateTimeZone exchangeTimeZone)
        {
            return new OpenInterest(ToExchangeTime(row.Timestamp, exchangeTimeZone), symbol, row.OpenInterest);
        }

        /// <summary>
        /// Orders data by time and removes repeated bars for the same symbol and start time.
        /// Ticks keep their duplicates, because 2 trades can share a timestamp.
        /// </summary>
        public static IEnumerable<BaseData> OrderAndRemoveDuplicates(IEnumerable<BaseData> data, Resolution resolution)
        {
            var ordered = data.OrderBy(x => x.Time);
            if (resolution == Resolution.Tick)
            {
                return ordered;
            }
            return ordered.DistinctBy(x => (x.Symbol, x.Time, x.DataType));
        }

        /// <summary>
        /// The trading day of an end-of-day row, as midnight in the exchange time zone.
        /// ThetaData builds the report after the close of that day, so the date of <see cref="EndOfDayRow.Created"/> is the trading day.
        /// </summary>
        public static DateTime ToTradingDay(EndOfDayRow row, DateTimeZone exchangeTimeZone)
        {
            return ToExchangeTime(row.Created.Date, exchangeTimeZone);
        }

        private static DateTime ToExchangeTime(DateTime thetaDataTime, DateTimeZone exchangeTimeZone)
            => thetaDataTime.ConvertTo(ThetaDataTimeZone, exchangeTimeZone);

        private static bool HasValidPrices(decimal open, decimal high, decimal low, decimal close)
            => open > 0 && high > 0 && low > 0 && close > 0;

        // The connector has always dropped a quote with an empty side. Issue #11 asks to keep 1-sided quotes.
        private static bool HasBothSides(decimal bid, decimal bidSize, decimal ask, decimal askSize)
            => bid > 0 && bidSize > 0 && ask > 0 && askSize > 0;

        private static QuoteBar? CreateQuoteBar(DateTime time, Symbol symbol, decimal bid, decimal bidSize, decimal ask, decimal askSize, TimeSpan period)
        {
            if (!HasBothSides(bid, bidSize, ask, askSize))
            {
                return null;
            }
            return new QuoteBar(time, symbol, new Bar(bid, bid, bid, bid), bidSize, new Bar(ask, ask, ask, ask), askSize, period);
        }
    }
}
