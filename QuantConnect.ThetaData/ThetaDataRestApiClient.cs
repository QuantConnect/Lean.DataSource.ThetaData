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
using QuantConnect.Configuration;
using QLNet;
using QuantConnect.Logging;

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

        public T? ExecuteRequest<T>(RestRequest request)
        {
            var response = _restClient.Execute(request);

            if (response == null)
            {
                throw new Exception($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}: No response for {request.Resource}");
            }

            if ((int)response.StatusCode == 472)
            {
                Log.Trace($"{nameof(ThetaDataRestApiClient)}.{nameof(ExecuteRequest)}:NO_DATA There was no data found for the specified request.");
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
