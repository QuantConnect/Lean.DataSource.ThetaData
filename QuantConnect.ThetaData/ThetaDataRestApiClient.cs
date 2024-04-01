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

using RestSharp;
using Newtonsoft.Json;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using QuantConnect.Lean.DataSource.ThetaData.Models.Interfaces;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    /// <summary>
    /// Represents a client for interacting with the Theta Data REST API by sending HTTP requests.
    /// </summary>
    public class ThetaDataRestApiClient
    {
        private readonly static string RestApiBaseUrl = Config.Get("polygon-api-url", "http://127.0.0.1:25510/v2");

        private readonly RestClient _restClient;

        public ThetaDataRestApiClient()
        {
            _restClient = new RestClient(RestApiBaseUrl);
        }

        public IEnumerable<T?> ExecuteRequest<T>(RestRequest? request) where T : IBaseResponse
        {
            while (request != null)
            {
                Log.Debug($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: URI: {_restClient.BuildUri(request)}");

                var response = _restClient.Execute(request);

                if (response == null || response.StatusCode == 0 || response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: No response received for request to {request.Resource}. Error message: {response?.ErrorMessage ?? "No error message available."}");
                }

                // docs: https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/3ucp87xxgy8d3-error-codes
                if ((int)response.StatusCode == 472)
                {
                    Log.Trace($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}:NO_DATA There was no data found for the specified request.");
                }

                var res = JsonConvert.DeserializeObject<T>(response.Content);

                yield return res;

                request = res?.Header.NextPage == null ? null : new RestRequest(res.Header.NextPage, Method.GET);
            };
        }
    }
}
