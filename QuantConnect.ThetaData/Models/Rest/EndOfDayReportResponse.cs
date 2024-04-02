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

using Newtonsoft.Json;
using QuantConnect.Lean.DataSource.ThetaData.Converters;

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

[JsonConverter(typeof(ThetaDataEndOfDayConverter))]
public readonly struct EndOfDayReportResponse
{
    /// <summary>
    /// Represents the time of day the report was generated
    /// </summary>
    //[JsonProperty("ms_of_day")]
    public uint ReportGeneratedTimeMilliseconds { get; }

    /// <summary>
    /// Represents the time of the last trade
    /// </summary>
    //[JsonProperty("ms_of_day2")]
    public uint LastTradeTimeMilliseconds { get; }

    /// <summary>
    /// The opening trade price.
    /// </summary>
    //[JsonProperty("open")]
    public decimal Open { get; }

    /// <summary>
    /// The highest traded price.
    /// </summary>
    //[JsonProperty("high")]
    public decimal High { get; }

    /// <summary>
    /// The lowest traded price.
    /// </summary>
    //[JsonProperty("low")]
    public decimal Low { get; }

    /// <summary>
    /// The closing traded price.
    /// </summary>
    //[JsonProperty("close")]
    public decimal Close { get; }

    /// <summary>
    /// The amount of contracts traded.
    /// </summary>
    //[JsonProperty("volume")]
    public decimal Volume { get; }

    /// <summary>
    /// The amount of trades.
    /// </summary>
    //[JsonProperty("count")]
    public uint AmountTrades { get; }

    /// <summary>
    /// The last NBBO bid size.
    /// </summary>
    //[JsonProperty("bid_size")]
    public decimal BidSize { get; }

    /// <summary>
    /// The last NBBO bid exchange.
    /// </summary>
    //[JsonProperty("bid_exchange")]
    public byte BidExchange { get; }

    /// <summary>
    /// The last NBBO bid price.
    /// </summary>
    //[JsonProperty("bid")]
    public decimal BidPrice { get; }

    /// <summary>
    /// The last NBBO bid condition.
    /// </summary>
    //[JsonProperty("bid_condition")]
    public string BidCondition { get; }

    /// <summary>
    /// The last NBBO ask size.
    /// </summary>
    //[JsonProperty("ask_size")]
    public decimal AskSize { get; }

    /// <summary>
    /// The last NBBO ask exchange.
    /// </summary>
    //[JsonProperty("ask_exchange")]
    public byte AskExchange { get; }

    /// <summary>
    /// The last NBBO ask price.
    /// </summary>
    //[JsonProperty("ask")]
    public decimal AskPrice { get; }

    /// <summary>
    /// The last NBBO ask condition.
    /// </summary>
    //[JsonProperty("ask_condition")]
    public string AskCondition { get; }

    /// <summary>
    /// The date formated as YYYYMMDD.
    /// </summary>
    //[JsonProperty("date")]
    public string Date { get; }

    //[JsonConstructor]
    public EndOfDayReportResponse(uint reportGeneratedTimeMilliseconds, uint lastTradeTimeMilliseconds, decimal open, decimal high, decimal low, decimal close,
        decimal volume, uint amountTrades, decimal bidSize, byte bidExchange, decimal bidPrice, string bidCondition, decimal askSize, byte askExchange,
        decimal askPrice, string askCondition, string date)
    {
        ReportGeneratedTimeMilliseconds = reportGeneratedTimeMilliseconds;
        LastTradeTimeMilliseconds = lastTradeTimeMilliseconds;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        AmountTrades = amountTrades;
        BidSize = bidSize;
        BidExchange = bidExchange;
        BidPrice = bidPrice;
        BidCondition = bidCondition;
        AskSize = askSize;
        AskExchange = askExchange;
        AskPrice = askPrice;
        AskCondition = askCondition;
        Date = date;
    }
}
