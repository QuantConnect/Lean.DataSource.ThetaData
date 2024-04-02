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

namespace QuantConnect.Lean.DataSource.ThetaData.Models.WebSocket;

public readonly struct WebSocketQuote
{
    [JsonProperty("ms_of_day")]
    public int DayTimeMilliseconds { get; }

    [JsonProperty("bid_size")]
    public int BidSize { get; }

    [JsonProperty("bid_exchange")]
    public byte BidExchange { get; }

    [JsonProperty("bid")]
    public decimal BidPrice { get; }

    [JsonProperty("bid_condition")]
    public int BidCondition { get; }

    [JsonProperty("ask_size")]
    public int AskSize { get; }

    [JsonProperty("ask_exchange")]
    public byte AskExchange { get; }

    [JsonProperty("ask")]
    public decimal AskPrice { get; }

    [JsonProperty("ask_condition")]
    public int AskCondition { get; }

    [JsonProperty("date")]
    public string Date { get; }

    [JsonConstructor]
    public WebSocketQuote(
        int dayTimeMilliseconds,
        int bidSize, byte bidExchange, decimal bidPrice, int bidCondition, int askSize, byte askExchange, decimal askPrice, int askCondition, string date)
    {
        DayTimeMilliseconds = dayTimeMilliseconds;
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
