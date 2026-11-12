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

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

/// <summary>
/// An option contract as the v3 REST API returns it in the <c>contract</c> object of an option response.
/// </summary>
/// <remarks>
/// The strike is in dollars (v2 used tenths of a cent). The right is "CALL" or "PUT".
/// Source: the v3 OpenAPI spec, https://thetadata.net/docs/openapiv3.yaml
/// </remarks>
public class ThetaDataContract
{
    /// <summary>
    /// The option root, for example "SPY" or "SPXW".
    /// </summary>
    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// The expiration date.
    /// </summary>
    [JsonProperty("expiration")]
    public DateTime Expiration { get; set; }

    /// <summary>
    /// The strike price in dollars.
    /// </summary>
    [JsonProperty("strike")]
    public decimal Strike { get; set; }

    /// <summary>
    /// "CALL" or "PUT".
    /// </summary>
    [JsonProperty("right")]
    public string Right { get; set; } = string.Empty;
}

/// <summary>
/// A row of a v3 history response. An option row also carries the contract it belongs to.
/// </summary>
public abstract class ThetaDataRow
{
    /// <summary>
    /// The option contract of this row, or null for a stock or index row.
    /// </summary>
    [JsonIgnore]
    public ThetaDataContract? Contract { get; set; }
}

/// <summary>
/// A row of <c>/v3/{stock|option|index}/history/eod</c>.
/// ThetaData builds this report at about 17:15 ET on each trading day.
/// </summary>
public class EndOfDayRow : ThetaDataRow
{
    /// <summary>
    /// The time ThetaData built the report, in ET. Its date is the trading day.
    /// </summary>
    [JsonProperty("created")]
    public DateTime Created { get; set; }

    /// <summary>
    /// The time of the last trade, in ET. It is midnight of the trading day when no trade happened.
    /// </summary>
    [JsonProperty("last_trade")]
    public DateTime LastTrade { get; set; }

    [JsonProperty("open")]
    public decimal Open { get; set; }

    [JsonProperty("high")]
    public decimal High { get; set; }

    [JsonProperty("low")]
    public decimal Low { get; set; }

    [JsonProperty("close")]
    public decimal Close { get; set; }

    [JsonProperty("volume")]
    public decimal Volume { get; set; }

    /// <summary>
    /// The number of trades.
    /// </summary>
    [JsonProperty("count")]
    public long Count { get; set; }

    [JsonProperty("bid_size")]
    public decimal BidSize { get; set; }

    [JsonProperty("bid_exchange")]
    public int BidExchange { get; set; }

    /// <summary>
    /// The last NBBO bid at report time. 0 means no bid.
    /// </summary>
    [JsonProperty("bid")]
    public decimal Bid { get; set; }

    [JsonProperty("bid_condition")]
    public int BidCondition { get; set; }

    [JsonProperty("ask_size")]
    public decimal AskSize { get; set; }

    [JsonProperty("ask_exchange")]
    public int AskExchange { get; set; }

    /// <summary>
    /// The last NBBO ask at report time. 0 means no ask.
    /// </summary>
    [JsonProperty("ask")]
    public decimal Ask { get; set; }

    [JsonProperty("ask_condition")]
    public int AskCondition { get; set; }
}

/// <summary>
/// A row of <c>/v3/{stock|option|index}/history/ohlc</c>. The timestamp is the start of the interval, in ET.
/// </summary>
public class OhlcRow : ThetaDataRow
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("open")]
    public decimal Open { get; set; }

    [JsonProperty("high")]
    public decimal High { get; set; }

    [JsonProperty("low")]
    public decimal Low { get; set; }

    [JsonProperty("close")]
    public decimal Close { get; set; }

    [JsonProperty("volume")]
    public decimal Volume { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }
}

/// <summary>
/// A row of <c>/v3/{stock|option}/history/quote</c>. A price of 0 means that side has no quote.
/// </summary>
public class QuoteRow : ThetaDataRow
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("bid_size")]
    public decimal BidSize { get; set; }

    [JsonProperty("bid_exchange")]
    public int BidExchange { get; set; }

    [JsonProperty("bid")]
    public decimal Bid { get; set; }

    [JsonProperty("bid_condition")]
    public int BidCondition { get; set; }

    [JsonProperty("ask_size")]
    public decimal AskSize { get; set; }

    [JsonProperty("ask_exchange")]
    public int AskExchange { get; set; }

    [JsonProperty("ask")]
    public decimal Ask { get; set; }

    [JsonProperty("ask_condition")]
    public int AskCondition { get; set; }
}

/// <summary>
/// A row of <c>/v3/{stock|option}/history/trade</c>.
/// </summary>
public class TradeRow : ThetaDataRow
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("condition")]
    public int Condition { get; set; }

    [JsonProperty("size")]
    public decimal Size { get; set; }

    [JsonProperty("exchange")]
    public int Exchange { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }
}

/// <summary>
/// A row of <c>/v3/option/history/open_interest</c>.
/// </summary>
public class OpenInterestRow : ThetaDataRow
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("open_interest")]
    public decimal OpenInterest { get; set; }
}

/// <summary>
/// A row of <c>/v3/option/list/contracts/{trade|quote}</c>: 1 contract per row.
/// </summary>
public class ContractListRow : ThetaDataRow
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonProperty("expiration")]
    public DateTime Expiration { get; set; }

    [JsonProperty("strike")]
    public decimal Strike { get; set; }

    [JsonProperty("right")]
    public string Right { get; set; } = string.Empty;

    /// <summary>
    /// The row as a contract.
    /// </summary>
    public ThetaDataContract ToContract() => new() { Symbol = Symbol, Expiration = Expiration, Strike = Strike, Right = Right };
}

/// <summary>
/// A row of <c>/v3/index/history/price</c>.
/// </summary>
public class PriceRow : ThetaDataRow
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }
}
