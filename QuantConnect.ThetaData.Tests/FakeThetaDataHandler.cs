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

using System;
using System.IO;
using System.Net;
using System.Web;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    /// <summary>
    /// An HTTP handler that answers ThetaData requests from a function and records each request.
    /// It needs no terminal, no account, and no network.
    /// </summary>
    public class FakeThetaDataHandler : HttpMessageHandler
    {
        private readonly Func<FakeRequest, HttpResponseMessage> _respond;
        private int _inFlight;

        /// <summary>
        /// Each request received, in order.
        /// </summary>
        public ConcurrentQueue<FakeRequest> Requests { get; } = new();

        /// <summary>
        /// The most requests that were in flight at the same time.
        /// </summary>
        public int MaxInFlight { get; private set; }

        /// <summary>
        /// A wait inside each request, to test concurrency limits.
        /// </summary>
        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public FakeThetaDataHandler(Func<FakeRequest, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var fakeRequest = new FakeRequest(request.RequestUri!);
            Requests.Enqueue(fakeRequest);
            var inFlight = Interlocked.Increment(ref _inFlight);
            lock (this)
            {
                MaxInFlight = Math.Max(MaxInFlight, inFlight);
            }
            try
            {
                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay, cancellationToken);
                }
                return _respond(fakeRequest);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        /// <summary>
        /// A response with a status code and a body.
        /// </summary>
        public static HttpResponseMessage Respond(int statusCode, string body)
            => new((HttpStatusCode)statusCode) { Content = new StringContent(body) };

        /// <summary>
        /// A 200 response with the content of a recorded fixture in TestData/v3.
        /// </summary>
        public static HttpResponseMessage Fixture(string fileName) => Respond(200, ReadFixture(fileName));

        /// <summary>
        /// Reads a recorded v3 response. The fixtures came from Theta Terminal v3 on 2026-10-28 (Free plan).
        /// </summary>
        public static string ReadFixture(string fileName) => File.ReadAllText(Path.Combine("TestData", "v3", fileName));

        /// <summary>
        /// 1 request, with its path relative to /v3/ and its query parameters.
        /// </summary>
        public class FakeRequest
        {
            public Uri Uri { get; }
            public string Path { get; }
            public NameValueCollection Query { get; }

            public FakeRequest(Uri uri)
            {
                Uri = uri;
                var absolutePath = uri.AbsolutePath;
                var index = absolutePath.IndexOf("/v3/", StringComparison.Ordinal);
                Path = index >= 0 ? absolutePath[(index + 4)..] : absolutePath.TrimStart('/');
                Query = HttpUtility.ParseQueryString(uri.Query);
            }

            public override string ToString() => Uri.ToString();
        }
    }
}
