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
using System.Globalization;

namespace QuantConnect.Lean.DataSource.ThetaData.Converters;

/// <summary>
/// Reads a v3 date or timestamp: "2024-01-19" or "2024-01-02T17:17:53.606".
/// The result has <see cref="DateTimeKind.Unspecified"/>, because v3 sends ET wall-clock time without an offset.
/// </summary>
public class ThetaDataDateTimeConverter : JsonConverter
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd",
        "yyyyMMdd"
    };

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType) => objectType == typeof(DateTime);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var text = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(text)
            || !DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new JsonSerializationException($"{nameof(ThetaDataDateTimeConverter)}: '{text}' is not a ThetaData date or timestamp.");
        }
        return value;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new NotSupportedException();
}
