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

public readonly struct WebSocketTrade
{
    [JsonProperty("ms_of_day")]
    public int DayTimeMilliseconds { get; }

    [JsonProperty("sequence")]
    public int Sequence { get; }

    [JsonProperty("size")]
    public int Size { get; }

    [JsonProperty("condition")]
    public int Condition { get; }

    [JsonProperty("price")]
    public decimal Price { get; }

    [JsonProperty("exchange")]
    public byte Exchange { get; }

    [JsonProperty("date")]
    public string Date { get; }

    [JsonConstructor]
    public WebSocketTrade(int dayTimeMilliseconds, int sequence, int size, int condition, decimal price, byte exchange, string date)
    {
        DayTimeMilliseconds = dayTimeMilliseconds;
        Sequence = sequence;
        Size = size;
        Condition = condition;
        Price = price;
        Exchange = exchange;
        Date = date;
    }
}
