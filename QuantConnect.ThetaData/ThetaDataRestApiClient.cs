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

using System.Net;
using System.Web;
using Newtonsoft.Json;
using QuantConnect.Util;
using Newtonsoft.Json.Linq;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using QuantConnect.Lean.DataSource.ThetaData.Converters;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Sends requests to the ThetaData v3 REST API that Theta Terminal v3 serves.
    /// </summary>
    /// <remarks>
    /// The terminal answers every v2 request with HTTP 410 (checked on 2026-10-28), so this client speaks v3 only.
    /// Docs: https://thetadata.net/docs/Articles/Getting-Started/v2-migration-guide.html
    /// </remarks>
    public class ThetaDataRestApiClient : IDisposable
    {
        /// <summary>
        /// The default address of Theta Terminal v3 on the same machine.
        /// </summary>
        public const string DefaultRestApiUrl = "http://127.0.0.1:25503";

        /// <summary>
        /// The port of the retired v2 terminal. A URL with this port is likely an old configuration.
        /// </summary>
        private const int LegacyV2Port = 25510;

        /// <summary>
        /// Days in 1 request for an interval below 1 minute. The v3 docs say:
        /// "Intervals less than 1m are available only for single-day requests."
        /// </summary>
        private const int SubMinuteRequestDays = 1;

        /// <summary>
        /// Days in 1 request for an interval of 1 minute or more. The v3 docs say:
        /// "Multi-day requests are limited to 1 month of data." 28 days fits in every month.
        /// </summary>
        private const int IntradayRequestDays = 28;

        /// <summary>
        /// Days in 1 request without an interval, such as end-of-day data. The terminal answered a longer range with
        /// HTTP 400 "Too many days between start and end date; max 365 days allowed" (2026-10-28).
        /// </summary>
        private const int MaxRequestDays = 365;

        private static readonly HashSet<string> SubMinuteIntervals = new() { "tick", "10ms", "100ms", "500ms", "1s", "5s", "10s", "15s", "30s" };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
            Converters = { new ThetaDataDateTimeConverter() }
        });

        private static bool _legacyPortWarningFired;

        private readonly HttpClient _httpClient;
        private readonly RateGate? _rateGate;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly int _maxConcurrentRequests;
        private readonly int _maxRetries;
        private readonly Func<int, TimeSpan> _retryDelay;
        private bool _disposed;

        /// <summary>
        /// The v3 base address, for example <c>http://127.0.0.1:25503/v3/</c>.
        /// </summary>
        public Uri BaseAddress { get; }

        /// <summary>
        /// Creates a client for the address in the <c>thetadata-rest-url</c> config key.
        /// </summary>
        /// <param name="rateGate">Limits the request rate, or null for no limit.</param>
        /// <param name="maxConcurrentRequests">The most requests in flight at once. ThetaData rejects more than the plan allows.</param>
        public ThetaDataRestApiClient(RateGate? rateGate, int maxConcurrentRequests = 1)
            : this(Config.Get("thetadata-rest-url", DefaultRestApiUrl), rateGate, maxConcurrentRequests)
        {
        }

        /// <summary>
        /// Creates a client for the given address.
        /// </summary>
        /// <param name="restApiUrl">The terminal address, with or without a trailing <c>/v3</c>.</param>
        /// <param name="rateGate">Limits the request rate, or null for no limit.</param>
        /// <param name="maxConcurrentRequests">The most requests in flight at once.</param>
        /// <param name="handler">The HTTP handler. Tests pass a fake one.</param>
        /// <param name="retryDelay">The wait before retry number n (1-based). Tests pass zero.</param>
        /// <param name="maxRetries">How many times a transient failure is retried.</param>
        public ThetaDataRestApiClient(string restApiUrl, RateGate? rateGate, int maxConcurrentRequests,
            HttpMessageHandler? handler = null, Func<int, TimeSpan>? retryDelay = null, int maxRetries = 3)
        {
            BaseAddress = GetApiBaseAddress(restApiUrl);
            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
            _httpClient.BaseAddress = BaseAddress;
            _httpClient.Timeout = TimeSpan.FromSeconds(Config.GetInt("thetadata-request-timeout-seconds", 300));
            _rateGate = rateGate;
            _maxConcurrentRequests = Math.Max(1, maxConcurrentRequests);
            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentRequests);
            _maxRetries = Math.Max(0, maxRetries);
            _retryDelay = retryDelay ?? DefaultRetryDelay;
        }

        /// <summary>
        /// Converts the configured terminal address to the v3 base address and rejects a v2 address.
        /// </summary>
        /// <exception cref="ArgumentException">The address is not an absolute HTTP address, or it names the v2 API.</exception>
        public static Uri GetApiBaseAddress(string restApiUrl)
        {
            var url = string.IsNullOrWhiteSpace(restApiUrl) ? DefaultRestApiUrl : restApiUrl.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"The 'thetadata-rest-url' value '{url}' is not an absolute http address. Example: {DefaultRestApiUrl}");
            }

            var path = uri.AbsolutePath.Trim('/');
            if (path.Equals("v2", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The 'thetadata-rest-url' value '{url}' names the ThetaData v2 API. ThetaData retired v2, and Theta Terminal v3 answers v2 requests with HTTP 410. " +
                    $"Set 'thetadata-rest-url' to the terminal address without a version, for example {DefaultRestApiUrl}, or http://host.docker.internal:25503 inside a LEAN container.");
            }
            if (path.Equals("v3", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/v3", StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^2].TrimEnd('/');
            }

            if (uri.Port == LegacyV2Port && !_legacyPortWarningFired)
            {
                _legacyPortWarningFired = true;
                Log.Error($"{nameof(ThetaDataRestApiClient)}: The 'thetadata-rest-url' value '{url}' uses port {LegacyV2Port}, the port of the retired v2 terminal. " +
                    "Theta Terminal v3 listens on port 25503 by default. If requests fail to connect, set 'thetadata-rest-url' to the v3 port.");
            }

            var prefix = path.Length == 0 ? string.Empty : path + "/";
            return new Uri($"{uri.Scheme}://{uri.Authority}/{prefix}v3/");
        }

        /// <summary>
        /// Sends a request and returns its rows. A request with no data returns no rows.
        /// </summary>
        /// <remarks>
        /// A request with both <c>start_date</c> and <c>end_date</c> and an <c>interval</c> is split into the date
        /// ranges that v3 accepts. The rows come back in date order of the ranges.
        /// </remarks>
        /// <exception cref="ThetaDataRequestException">The request failed, or the plan does not include the data.</exception>
        public IReadOnlyList<T> GetRows<T>(string endpoint, IReadOnlyDictionary<string, string> queryParameters) where T : ThetaDataRow
        {
            return GetRowsAsync<T>(endpoint, queryParameters, CancellationToken.None).SynchronouslyAwaitTaskResult();
        }

        /// <inheritdoc cref="GetRows{T}"/>
        public async Task<IReadOnlyList<T>> GetRowsAsync<T>(string endpoint, IReadOnlyDictionary<string, string> queryParameters, CancellationToken cancellationToken)
            where T : ThetaDataRow
        {
            var requests = SplitByDateRange(queryParameters).ToList();
            if (requests.Count == 1)
            {
                return await GetRowsOfOneRequestAsync<T>(endpoint, requests[0], cancellationToken).ConfigureAwait(false);
            }

            var results = new List<T>[requests.Count];
            await Parallel.ForEachAsync(Enumerable.Range(0, requests.Count),
                new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrentRequests, CancellationToken = cancellationToken },
                async (index, token) => results[index] = await GetRowsOfOneRequestAsync<T>(endpoint, requests[index], token).ConfigureAwait(false))
                .ConfigureAwait(false);
            return results.SelectMany(x => x).ToList();
        }

        /// <summary>
        /// Splits a request into the date ranges that v3 accepts for its interval.
        /// </summary>
        public static IEnumerable<IReadOnlyDictionary<string, string>> SplitByDateRange(IReadOnlyDictionary<string, string> queryParameters)
        {
            if (!queryParameters.TryGetValue(RequestParameters.StartDate, out var start)
                || !queryParameters.TryGetValue(RequestParameters.EndDate, out var end))
            {
                yield return queryParameters;
                yield break;
            }

            var days = !queryParameters.TryGetValue(RequestParameters.Interval, out var interval) ? MaxRequestDays
                : SubMinuteIntervals.Contains(interval) ? SubMinuteRequestDays
                : IntradayRequestDays;
            foreach (var (startDate, endDate) in ThetaDataExtensions.GenerateDateRangesWithInterval(start.ConvertFromThetaDataDateFormat(), end.ConvertFromThetaDataDateFormat(), days))
            {
                var parameters = new Dictionary<string, string>(queryParameters)
                {
                    [RequestParameters.StartDate] = startDate.ConvertToThetaDataDateFormat(),
                    [RequestParameters.EndDate] = endDate.ConvertToThetaDataDateFormat()
                };
                yield return parameters;
            }
        }

        /// <summary>
        /// Parses a v3 JSON response. Stock and index rows are flat. Option rows come as
        /// <c>{"contract": {...}, "data": [...]}</c>, one entry per contract.
        /// </summary>
        /// <exception cref="ThetaDataRequestException">The body is not a v3 JSON response.</exception>
        public static List<T> ParseRows<T>(string json) where T : ThetaDataRow
        {
            JToken root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal
                };
                root = JToken.ReadFrom(reader);
            }
            catch (JsonReaderException e)
            {
                throw new ThetaDataRequestException($"{nameof(ThetaDataRestApiClient)}: The response is not JSON. It starts with: '{Truncate(json)}'", innerException: e);
            }

            var items = root as JArray ?? (root as JObject)?["response"] as JArray
                ?? throw new ThetaDataRequestException($"{nameof(ThetaDataRestApiClient)}: The response has no 'response' array. It starts with: '{Truncate(json)}'");

            var rows = new List<T>(items.Count);
            try
            {
                foreach (var item in items)
                {
                    if (item is JObject entry && entry["data"] is JArray data && entry["contract"] is JObject contractToken)
                    {
                        var contract = contractToken.ToObject<ThetaDataContract>(Serializer)!;
                        foreach (var dataItem in data)
                        {
                            var row = dataItem.ToObject<T>(Serializer)!;
                            row.Contract = contract;
                            rows.Add(row);
                        }
                    }
                    else
                    {
                        rows.Add(item.ToObject<T>(Serializer)!);
                    }
                }
            }
            catch (Exception e) when (e is JsonException or ArgumentException or FormatException or OverflowException)
            {
                throw new ThetaDataRequestException($"{nameof(ThetaDataRestApiClient)}: A row of the response does not match {typeof(T).Name}: {e.Message} The response starts with: '{Truncate(json)}'", innerException: e);
            }
            return rows;
        }

        private async Task<List<T>> GetRowsOfOneRequestAsync<T>(string endpoint, IReadOnlyDictionary<string, string> queryParameters, CancellationToken cancellationToken)
            where T : ThetaDataRow
        {
            var content = await SendAsync(BuildRequestUri(endpoint, queryParameters), cancellationToken).ConfigureAwait(false);
            return content == null ? new List<T>() : ParseRows<T>(content);
        }

        /// <summary>
        /// Builds the relative request address. It always asks for JSON.
        /// </summary>
        public static Uri BuildRequestUri(string endpoint, IReadOnlyDictionary<string, string> queryParameters)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var (key, value) in queryParameters)
            {
                query[key] = value;
            }
            query[RequestParameters.Format] = "json";
            return new Uri($"{endpoint.TrimStart('/')}?{query}", UriKind.Relative);
        }

        /// <summary>
        /// Sends 1 GET request. Returns the body, or null when ThetaData has no data (HTTP 472).
        /// </summary>
        private async Task<string?> SendAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                int? statusCode = null;
                string failure;
                Exception? innerException = null;

                await _concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _rateGate?.WaitToProceed();
                    Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(SendAsync)}: GET {requestUri}");
                    using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    statusCode = (int)response.StatusCode;

                    switch (statusCode)
                    {
                        case 200:
                            return body;
                        // docs: https://thetadata.net/docs/Articles/Errors-Exchanges-Conditions/Error-Codes.html
                        case 472:
                            Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(SendAsync)}: No data for {requestUri}");
                            return null;
                        case 403:
                        case 471:
                            throw new ThetaDataRequestException($"ThetaData denied {requestUri} (HTTP {statusCode}): {Truncate(body)}. Your ThetaData plan does not include this data.", statusCode);
                        case 410:
                            throw new ThetaDataRequestException($"Theta Terminal at {BaseAddress} rejected {requestUri} as retired (HTTP 410): {Truncate(body)}", statusCode);
                        case 570:
                            throw new ThetaDataRequestException($"ThetaData rejected {requestUri} as too large (HTTP 570). Request a shorter date range.", statusCode);
                        case 429:
                        case 474:
                        case 571:
                        case >= 500:
                            failure = $"HTTP {statusCode}: {Truncate(body)}";
                            break;
                        default:
                            throw new ThetaDataRequestException($"ThetaData rejected {requestUri} (HTTP {statusCode}): {Truncate(body)}", statusCode);
                    }
                }
                catch (HttpRequestException e)
                {
                    failure = $"cannot connect to Theta Terminal at {BaseAddress}: {e.Message}. Check that Theta Terminal v3 runs. " +
                        "Inside a LEAN container, set 'thetadata-rest-url' to http://host.docker.internal:25503";
                    innerException = e;
                }
                catch (TaskCanceledException e) when (!cancellationToken.IsCancellationRequested)
                {
                    failure = $"timeout after {_httpClient.Timeout.TotalSeconds} seconds";
                    innerException = e;
                }
                finally
                {
                    _concurrencyLimiter.Release();
                }

                if (attempt >= _maxRetries)
                {
                    throw new ThetaDataRequestException($"ThetaData request {requestUri} failed after {attempt + 1} attempts: {failure}", statusCode, innerException);
                }
                Log.Trace($"{nameof(ThetaDataRestApiClient)}.{nameof(SendAsync)}: Retry {attempt + 1} of {_maxRetries} for {requestUri}: {failure}");
                await Task.Delay(_retryDelay(attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Exponential backoff with jitter: about 1 s, 2 s, 4 s, plus up to 250 ms.
        /// </summary>
        private static TimeSpan DefaultRetryDelay(int retry)
            => TimeSpan.FromMilliseconds(1000 * Math.Pow(2, retry - 1) + Random.Shared.Next(0, 250));

        private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];

        /// <summary>
        /// Disposes the HTTP client.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _httpClient.Dispose();
            _concurrencyLimiter.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
