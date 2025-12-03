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
using QuantConnect.Util;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using System.Collections.Concurrent;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;
using QuantConnect.Lean.DataSource.ThetaData.Models.Wrappers;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Represents a client for interacting with the Theta Data REST API by sending HTTP requests.
    /// </summary>
    public class ThetaDataRestApiClient : IDisposable
    {
        /// <summary>
        /// The maximum number of times a failed request will be retried.
        /// </summary>
        private const int MaxRequestRetries = 2;

        /// <summary>
        /// Represents the API version used in the REST API endpoints.
        /// </summary>
        /// <remarks>
        /// This constant defines the version of the API to be used in requests. 
        /// It is appended to the base URL to form the complete endpoint path.
        /// </remarks>
        private const string ApiVersion = "/v2";

        /// <summary>
        /// Represents the base URL for the REST API.
        /// </summary>
        private readonly string _restApiBaseUrl = Config.Get("thetadata-rest-url", "http://127.0.0.1:25510");

        /// <summary>
        /// Represents a client for making HTTP API requests.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Represents a RateGate instance used to control the rate of certain operations.
        /// </summary>
        private readonly RateGate? _rateGate;

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataRestApiClient"/>
        /// </summary>
        /// <param name="rateGate">Rate gate for controlling request rate.</param>
        public ThetaDataRestApiClient(RateGate rateGate)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_restApiBaseUrl + ApiVersion),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _rateGate = rateGate;
        }

        /// <summary>
        /// Executes a REST request in parallel and returns the results synchronously.
        /// </summary>
        /// <typeparam name="T">The type of object that implements the <see cref="IBaseResponse"/> interface.</typeparam>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="queryParameters">Query parameters for the request.</param>
        /// <returns>A collection of objects that implement the <see cref="IBaseResponse"/> interface.</returns>
        public IEnumerable<T?> ExecuteRequest<T>(string endpoint, Dictionary<string, string> queryParameters) where T : IBaseResponse
        {
            var parameters = GetSpecificQueryParameters(queryParameters, RequestParameters.IntervalInMilliseconds, RequestParameters.StartDate, RequestParameters.EndDate);

            if (parameters.Count != 3)
            {
                return ExecuteRequestAsync<T>(endpoint, queryParameters).SynchronouslyAwaitTaskResult();
            }

            var intervalInDay = parameters[RequestParameters.IntervalInMilliseconds] switch
            {
                "0" => 1,
                "1000" or "60000" => 30,
                "3600000" => 90,
                _ => throw new NotImplementedException($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequestParallelAsync)}: The interval '{parameters[RequestParameters.IntervalInMilliseconds]}' is not supported.")
            };

            var startDate = parameters[RequestParameters.StartDate].ConvertFromThetaDataDateFormat();
            var endDate = parameters[RequestParameters.EndDate].ConvertFromThetaDataDateFormat();

            if ((endDate - startDate).TotalDays <= intervalInDay)
            {
                return ExecuteRequestAsync<T>(endpoint, queryParameters).SynchronouslyAwaitTaskResult();
            }

            return ExecuteRequestParallelAsync<T>(endpoint, queryParameters, startDate, endDate, intervalInDay).SynchronouslyAwaitTaskResult();
        }

        /// <summary>
        /// Executes an HTTP GET request and deserializes the response content into an object.
        /// </summary>
        /// <typeparam name="T">The type of objects that implement the base response interface.</typeparam>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="queryParameters">Query parameters for the request.</param>
        /// <returns>An enumerable collection of objects that implement the specified base response interface.</returns>
        /// <exception cref="Exception">Thrown when an error occurs during the execution of the request or when the response is invalid.</exception>
        private async IAsyncEnumerable<T?> ExecuteRequestWithPaginationAsync<T>(string endpoint, Dictionary<string, string> queryParameters) where T : IBaseResponse
        {
            var retryCount = 0;
            var currentEndpoint = endpoint;
            var currentQueryParams = new Dictionary<string, string>(queryParameters);

            while (currentEndpoint != null)
            {
                var requestUri = BuildRequestUri(currentEndpoint, currentQueryParams);
                Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: URI: {requestUri}");

                _rateGate?.WaitToProceed();

                T? result = default;
                bool shouldBreak = false;
                bool shouldContinue = false;

                using (StopwatchWrapper.StartIfEnabled($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: Executed request to {currentEndpoint}"))
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(requestUri).ConfigureAwait(false);

                        // docs: https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/3ucp87xxgy8d3-error-codes
                        if ((int)response.StatusCode == 472)
                        {
                            Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}:No data found for the specified request (Status Code: 472) by {requestUri}");
                            shouldBreak = true;
                        }
                        else if (!response.IsSuccessStatusCode)
                        {
                            if (retryCount < MaxRequestRetries)
                            {
                                retryCount++;
                                await Task.Delay(1000 * retryCount).ConfigureAwait(false);
                                shouldContinue = true;
                            }
                            else
                            {
                                throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: Request failed with status code {response.StatusCode} for {currentEndpoint}. Reason: {response.ReasonPhrase}");
                            }
                        }
                        else
                        {
                            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            result = JsonConvert.DeserializeObject<T>(content);

                            if (result?.Header.NextPage != null)
                            {
                                var nextPageUri = new Uri(result.Header.NextPage);
                                currentEndpoint = nextPageUri.AbsolutePath.Replace(ApiVersion, string.Empty);
                                currentQueryParams = ParseQueryString(nextPageUri.Query);
                            }
                            else
                            {
                                currentEndpoint = null;
                            }

                            retryCount = 0; // Reset retry count on success
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        if (retryCount < MaxRequestRetries)
                        {
                            retryCount++;
                            await Task.Delay(1000 * retryCount).ConfigureAwait(false);
                            shouldContinue = true;
                        }
                        else
                        {
                            throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: HTTP request failed for {currentEndpoint}. Error: {ex.Message}", ex);
                        }
                    }
                    catch (TaskCanceledException ex)
                    {
                        throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: Request timeout for {currentEndpoint}. Error: {ex.Message}", ex);
                    }
                }

                if (shouldBreak)
                {
                    yield break;
                }

                if (shouldContinue)
                {
                    continue;
                }

                if (result != null)
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Executes a REST request asynchronously and retrieves a paginated response.
        /// </summary>
        /// <typeparam name="T">The type of response that implements <see cref="IBaseResponse"/>.</typeparam>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="queryParameters">Query parameters for the request.</param>
        /// <returns>
        /// A task that represents the asynchronous operation, returning an <see cref="IEnumerable{T}"/> 
        /// containing the responses received from paginated requests.
        /// </returns>
        private async Task<IEnumerable<T?>> ExecuteRequestAsync<T>(string endpoint, Dictionary<string, string> queryParameters) where T : IBaseResponse
        {
            var responses = new List<T?>();
            await foreach (var response in ExecuteRequestWithPaginationAsync<T>(endpoint, queryParameters))
            {
                responses.Add(response);
            }
            return responses;
        }

        /// <summary>
        /// Executes a REST request in parallel over multiple date ranges, ensuring efficient batch processing.
        /// A maximum of 4 parallel requests are made at a time to avoid excessive API load.
        /// </summary>
        /// <typeparam name="T">The type of response that implements <see cref="IBaseResponse"/>.</typeparam>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="queryParameters">Query parameters for the request.</param>
        /// <param name="startDate">The start date of the data range.</param>
        /// <param name="endDate">The end date of the data range.</param>
        /// <param name="intervalInDay">
        /// The interval in days for splitting the date range into smaller requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, returning an <see cref="IEnumerable{T}"/> 
        /// containing the aggregated responses from all parallel requests.
        /// </returns>
        private async Task<IEnumerable<T?>> ExecuteRequestParallelAsync<T>(string endpoint, Dictionary<string, string> queryParameters, DateTime startDate, DateTime endDate, int intervalInDay) where T : IBaseResponse
        {
            var resultDict = new ConcurrentDictionary<int, List<T?>>();

            var dateRanges = ThetaDataExtensions.GenerateDateRangesWithInterval(startDate, endDate, intervalInDay).Select((range, index) => (range, index)).ToList();

            await Parallel.ForEachAsync(dateRanges, async (item, _) =>
            {
                var (dateRange, index) = item;
                var modifiedParams = new Dictionary<string, string>(queryParameters);

                modifiedParams[RequestParameters.StartDate] = dateRange.startDate.ConvertToThetaDataDateFormat();
                modifiedParams[RequestParameters.EndDate] = dateRange.endDate.ConvertToThetaDataDateFormat();

                var results = new List<T?>();
                await foreach (var response in ExecuteRequestWithPaginationAsync<T>(endpoint, modifiedParams))
                {
                    results.Add(response);
                }
                resultDict[index] = results;
            }).ConfigureAwait(false);

            return resultDict.OrderBy(kvp => kvp.Key).SelectMany(kvp => kvp.Value);
        }

        /// <summary>
        /// Builds a complete request URI with query parameters.
        /// </summary>
        /// <param name="endpoint">The API endpoint.</param>
        /// <param name="queryParameters">Query parameters to append.</param>
        /// <returns>The complete URI string.</returns>
        private string BuildRequestUri(string endpoint, Dictionary<string, string> queryParameters)
        {
            if (queryParameters == null || queryParameters.Count == 0)
            {
                return endpoint;
            }

            var queryString = string.Join("&", queryParameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            return $"{endpoint}?{queryString}";
        }

        /// <summary>
        /// Parses a query string into a dictionary of parameters.
        /// </summary>
        /// <param name="queryString">The query string to parse.</param>
        /// <returns>A dictionary of query parameters.</returns>
        private Dictionary<string, string> ParseQueryString(string queryString)
        {
            var result = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(queryString))
            {
                return result;
            }

            queryString = queryString.TrimStart('?');
            var pairs = queryString.Split('&');

            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    result[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts specific query parameters from a collection of request parameters.
        /// </summary>
        /// <param name="queryParameters">The dictionary of query parameters.</param>
        /// <param name="findingParamNames">The parameter names to find.</param>
        /// <returns>A dictionary of the matching query parameters and their values.</returns>
        /// <exception cref="ArgumentException">Thrown when a required parameter is missing or has an invalid value.</exception>
        private Dictionary<string, string> GetSpecificQueryParameters(Dictionary<string, string> queryParameters, params string[] findingParamNames)
        {
            var parameters = new Dictionary<string, string>(findingParamNames.Length);

            foreach (var paramName in findingParamNames)
            {
                if (queryParameters.TryGetValue(paramName, out var value))
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new ArgumentException($"The value for the parameter '{paramName}' is null or empty. Ensure that this parameter has a valid value.", nameof(queryParameters));
                    }
                    parameters[paramName] = value;
                }
            }

            return parameters;
        }

        /// <summary>
        /// Disposes the HTTP client resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the HTTP client resources.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}