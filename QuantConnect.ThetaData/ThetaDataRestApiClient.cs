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

using RestSharp;
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
    public class ThetaDataRestApiClient
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
        private readonly string RestApiBaseUrl = Config.Get("thetadata-rest-url", "http://127.0.0.1:25510");

        /// <summary>
        /// Represents a client for making RESTFul API requests.
        /// </summary>
        private readonly RestClient _restClient;

        /// <summary>
        /// Represents a RateGate instance used to control the rate of certain operations.
        /// </summary>
        private readonly RateGate? _rateGate;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataRestApiClient"/>
        /// </summary>
        /// <param name="subscriptionPlan">User's ThetaData subscription price plan.</param>
        public ThetaDataRestApiClient(RateGate rateGate)
        {
            _restClient = new RestClient(RestApiBaseUrl + ApiVersion);
            _rateGate = rateGate;
        }

        /// <summary>
        /// Executes a REST request in parallel and returns the results synchronously.
        /// </summary>
        /// <typeparam name="T">The type of object that implements the <see cref="IBaseResponse"/> interface.</typeparam>
        /// <param name="request">The REST request to execute.</param>
        /// <returns>A collection of objects that implement the <see cref="IBaseResponse"/> interface.</returns>
        public IEnumerable<T?> ExecuteRequest<T>(RestRequest? request) where T : IBaseResponse
        {
            var parameters = GetSpecificQueryParameters(request.Parameters, RequestParameters.IntervalInMilliseconds, RequestParameters.StartDate, RequestParameters.EndDate);

            if (parameters.Count != 3)
            {
                return ExecuteRequestAsync<T>(request).SynchronouslyAwaitTaskResult();
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
                return ExecuteRequestAsync<T>(request).SynchronouslyAwaitTaskResult();
            }

            return ExecuteRequestParallelAsync<T>(request, startDate, endDate, intervalInDay).SynchronouslyAwaitTaskResult();
        }

        /// <summary>
        /// Executes a REST request and deserializes the response content into an object.
        /// </summary>
        /// <typeparam name="T">The type of objects that implement the base response interface.</typeparam>
        /// <param name="request">The REST request to execute.</param>
        /// <returns>An enumerable collection of objects that implement the specified base response interface.</returns>
        /// <exception cref="Exception">Thrown when an error occurs during the execution of the request or when the response is invalid.</exception>
        private async IAsyncEnumerable<T?> ExecuteRequestWithPaginationAsync<T>(RestRequest? request) where T : IBaseResponse
        {
            var retryCount = 0;
            while (request != null)
            {
                Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: URI: {_restClient.BuildUri(request)}");

                _rateGate?.WaitToProceed();

                using (StopwatchWrapper.StartIfEnabled($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: Executed request to {request.Resource}"))
                {
                    var response = await _restClient.ExecuteAsync(request).ConfigureAwait(false);

                    // docs: https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/3ucp87xxgy8d3-error-codes
                    if ((int)response.StatusCode == 472)
                    {
                        Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}:No data found for the specified request (Status Code: 472) by {response.ResponseUri}");
                        yield break;
                    }

                    if (response == null || response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        if (retryCount < MaxRequestRetries)
                        {
                            retryCount++;
                            await Task.Delay(1000 * retryCount).ConfigureAwait(false);
                            continue;
                        }
                        throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: No response received for request to {request.Resource}. Error message: {response?.ErrorMessage ?? "No error message available."}");
                    }

                    var res = JsonConvert.DeserializeObject<T>(response.Content);

                    yield return res;

                    if (res?.Header.NextPage != null)
                    {
                        request = new RestRequest(Method.GET) { Resource = new Uri(res.Header.NextPage).AbsolutePath.Replace(ApiVersion, string.Empty) };
                    }
                    else
                    {
                        request = null;

                    }
                }
            }
        }

        /// <summary>
        /// Executes a REST request asynchronously and retrieves a paginated response.
        /// </summary>
        /// <typeparam name="T">The type of response that implements <see cref="IBaseResponse"/>.</typeparam>
        /// <param name="request">The REST request to execute.</param>
        /// <returns>
        /// A task that represents the asynchronous operation, returning an <see cref="IEnumerable{T}"/> 
        /// containing the responses received from paginated requests.
        /// </returns>
        private async Task<IEnumerable<T?>> ExecuteRequestAsync<T>(RestRequest? request) where T : IBaseResponse
        {
            var responses = new List<T?>();
            await foreach (var response in ExecuteRequestWithPaginationAsync<T>(request))
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
        /// <param name="request">The REST request to execute.</param>
        /// <param name="startDate">The start date of the data range.</param>
        /// <param name="endDate">The end date of the data range.</param>
        /// <param name="intervalInDay">
        /// The interval in days for splitting the date range into smaller requests.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation, returning an <see cref="IEnumerable{T}"/> 
        /// containing the aggregated responses from all parallel requests.
        /// </returns>
        private async Task<IEnumerable<T?>> ExecuteRequestParallelAsync<T>(RestRequest? request, DateTime startDate, DateTime endDate, int intervalInDay) where T : IBaseResponse
        {
            var resultDict = new ConcurrentDictionary<int, List<T?>>();

            var dateRanges = ThetaDataExtensions.GenerateDateRangesWithInterval(startDate, endDate, intervalInDay).Select((range, index) => (range, index)).ToList();

            await Parallel.ForEachAsync(dateRanges, async (item, _) =>
            {
                var (dateRange, index) = item;
                var requestClone = new RestRequest(request.Resource, request.Method);

                foreach (var param in request.Parameters)
                {
                    switch (param.Name)
                    {
                        case RequestParameters.StartDate:
                            requestClone.AddOrUpdateParameter(RequestParameters.StartDate, dateRange.startDate.ConvertToThetaDataDateFormat(), ParameterType.QueryString);
                            break;
                        case RequestParameters.EndDate:
                            requestClone.AddOrUpdateParameter(RequestParameters.EndDate, dateRange.endDate.ConvertToThetaDataDateFormat(), ParameterType.QueryString);
                            break;
                        default:
                            requestClone.AddParameter(param);
                            break;
                    }
                }

                var results = new List<T?>();
                await foreach (var response in ExecuteRequestWithPaginationAsync<T>(requestClone))
                {
                    results.Add(response);
                }
                resultDict[index] = results;
            }).ConfigureAwait(false);
            return resultDict.OrderBy(kvp => kvp.Key).SelectMany(kvp => kvp.Value);
        }

        /// <summary>
        /// Extracts specific query parameters from a collection of request parameters.
        /// </summary>
        /// <param name="requestParameters">The collection of request parameters.</param>
        /// <param name="findingParamNames">The parameter names to find.</param>
        /// <returns>A dictionary of the matching query parameters and their values.</returns>
        /// <exception cref="ArgumentException">Thrown when a required parameter is missing or has an invalid value.</exception>
        private Dictionary<string, string> GetSpecificQueryParameters(IReadOnlyCollection<Parameter> requestParameters, params string[] findingParamNames)
        {
            var parameters = new Dictionary<string, string>(findingParamNames.Length);
            foreach (var parameter in requestParameters)
            {
                if (parameter?.Name != null && findingParamNames.Contains(parameter.Name, StringComparer.InvariantCultureIgnoreCase))
                {
                    var value = parameter.Value?.ToString();
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new ArgumentException($"The value for the parameter '{parameter.Name}' is null or empty. Ensure that this parameter has a valid value.", nameof(requestParameters));
                    }

                    parameters[parameter.Name] = value;
                }
            }
            return parameters;
        }
    }
}
