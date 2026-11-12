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
using System.Linq;
using System.Net.Http;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Collections.Generic;
using QuantConnect.Lean.DataSource.ThetaData.Models.Rest;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests
{
    [TestFixture]
    public class ThetaDataRestApiClientTests
    {
        private static ThetaDataRestApiClient CreateClient(FakeThetaDataHandler handler, int maxConcurrentRequests = 1, int maxRetries = 3)
            => new("http://127.0.0.1:25503", null, maxConcurrentRequests, handler, _ => TimeSpan.Zero, maxRetries);

        private static readonly Dictionary<string, string> SpyEod = new()
        {
            [RequestParameters.Symbol] = "SPY",
            [RequestParameters.StartDate] = "20261019",
            [RequestParameters.EndDate] = "20261020"
        };

        [TestCase("", "http://127.0.0.1:25503/v3/")]
        [TestCase("http://127.0.0.1:25503", "http://127.0.0.1:25503/v3/")]
        [TestCase("http://127.0.0.1:25503/", "http://127.0.0.1:25503/v3/")]
        [TestCase("http://127.0.0.1:25503/v3", "http://127.0.0.1:25503/v3/")]
        [TestCase("http://host.docker.internal:25503/v3/", "http://host.docker.internal:25503/v3/")]
        [TestCase("https://proxy.example/theta", "https://proxy.example/theta/v3/")]
        [TestCase("http://127.0.0.1:25510", "http://127.0.0.1:25510/v3/")]
        public void BuildsTheV3BaseAddress(string configured, string expected)
        {
            Assert.AreEqual(expected, ThetaDataRestApiClient.GetApiBaseAddress(configured).ToString());
        }

        [TestCase("http://127.0.0.1:25510/v2")]
        [TestCase("http://127.0.0.1:25510/v2/")]
        [TestCase("not a url")]
        [TestCase("ftp://127.0.0.1:25503")]
        public void RejectsAnAddressItCannotUse(string configured)
        {
            Assert.Throws<ArgumentException>(() => ThetaDataRestApiClient.GetApiBaseAddress(configured));
        }

        [Test]
        public void BuildsTheRequestWithV3NamesAndJsonFormat()
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Fixture("stock_eod_spy_20261019_20261020.json"));
            using var client = CreateClient(handler);

            client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod);

            var request = handler.Requests.Single();
            Assert.AreEqual("http://127.0.0.1:25503/v3/stock/history/eod", request.Uri.GetLeftPart(UriPartial.Path));
            Assert.AreEqual("SPY", request.Query["symbol"]);
            Assert.AreEqual("20261019", request.Query["start_date"]);
            Assert.AreEqual("20261020", request.Query["end_date"]);
            Assert.AreEqual("json", request.Query["format"]);
            Assert.IsNull(request.Query["root"], "v2 parameter name");
        }

        [Test]
        public void ParsesAFlatStockResponseWithExactDecimals()
        {
            var rows = ThetaDataRestApiClient.ParseRows<EndOfDayRow>(FakeThetaDataHandler.ReadFixture("stock_eod_spy_20261019_20261020.json"));

            Assert.AreEqual(2, rows.Count);
            Assert.IsNull(rows[0].Contract);
            Assert.AreEqual(new DateTime(2026, 10, 19), rows[0].Created.Date);
            Assert.AreEqual(DateTimeKind.Unspecified, rows[0].Created.Kind);
            Assert.AreEqual(new DateTime(2026, 10, 20), rows[1].Created.Date);
            Assert.That(rows.All(x => x.Open > 0 && x.High >= x.Low && x.Close > 0 && x.Volume > 0 && x.Count > 0));
        }

        [Test]
        public void ParsesANestedOptionResponseAndAttachesTheContract()
        {
            var rows = ThetaDataRestApiClient.ParseRows<EndOfDayRow>(FakeThetaDataHandler.ReadFixture("option_eod_spy_20240119_320c_20240102_20240103.json"));

            Assert.AreEqual(2, rows.Count);
            foreach (var row in rows)
            {
                Assert.AreEqual("SPY", row.Contract!.Symbol);
                Assert.AreEqual(new DateTime(2024, 1, 19), row.Contract.Expiration);
                Assert.AreEqual(320m, row.Contract.Strike);
                Assert.AreEqual("CALL", row.Contract.Right);
            }
            // 2024-01-02: no trade, so OHLC is 0, but the NBBO is real.
            Assert.AreEqual(0, rows[0].Count);
            Assert.AreEqual(0m, rows[0].Close);
            Assert.AreEqual(153.38m, rows[0].Bid);
            Assert.AreEqual(153.93m, rows[0].Ask);
            Assert.AreEqual(new DateTime(2024, 1, 2, 17, 25, 19, 675), rows[0].Created);
            // 2024-01-03: 1 trade of 3 contracts at 151.08.
            Assert.AreEqual(151.08m, rows[1].Close);
            Assert.AreEqual(3m, rows[1].Volume);
            Assert.AreEqual(new DateTime(2024, 1, 3, 9, 32, 43, 359), rows[1].LastTrade);
        }

        [Test]
        public void ParsesABulkResponseOfManyContracts()
        {
            var rows = ThetaDataRestApiClient.ParseRows<EndOfDayRow>(FakeThetaDataHandler.ReadFixture("option_eod_spy_bulk_20261020_sample.json"));

            Assert.AreEqual(6, rows.Count);
            Assert.AreEqual(6, rows.Select(x => (x.Contract!.Expiration, x.Contract.Strike, x.Contract.Right)).Distinct().Count());
        }

        [TestCase("No data found for your request")]
        [TestCase("")]
        public void ReturnsNoRowsForNoDataAndDoesNotRetry(string body)
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(472, body));
            using var client = CreateClient(handler);

            var rows = client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod);

            Assert.IsEmpty(rows);
            Assert.AreEqual(1, handler.Requests.Count);
        }

        [TestCase(403)]
        [TestCase(471)]
        public void ThrowsAnEntitlementErrorWithoutRetry(int statusCode)
        {
            const string body = "Requesting an option endpoint requiring a value subscription, but you only have a FREE subscription.";
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(statusCode, body));
            using var client = CreateClient(handler);

            var exception = Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));

            Assert.IsTrue(exception!.IsEntitlementError);
            Assert.AreEqual(statusCode, exception.StatusCode);
            StringAssert.Contains("FREE subscription", exception.Message);
            Assert.AreEqual(1, handler.Requests.Count);
        }

        [TestCase(410, "We have upgraded to API v3.")]
        [TestCase(473, "invalid params")]
        [TestCase(404, "not found")]
        [TestCase(570, "too large")]
        public void ThrowsWithoutRetryForAPermanentError(int statusCode, string body)
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(statusCode, body));
            using var client = CreateClient(handler);

            var exception = Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));

            Assert.AreEqual(statusCode, exception!.StatusCode);
            Assert.IsFalse(exception.IsEntitlementError);
            Assert.AreEqual(1, handler.Requests.Count);
        }

        [TestCase(429)]
        [TestCase(474)]
        [TestCase(571)]
        [TestCase(503)]
        public void RetriesATransientError(int statusCode)
        {
            var calls = 0;
            var handler = new FakeThetaDataHandler(_ => ++calls == 1
                ? FakeThetaDataHandler.Respond(statusCode, "busy")
                : FakeThetaDataHandler.Fixture("stock_eod_spy_20261019_20261020.json"));
            using var client = CreateClient(handler);

            var rows = client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(2, handler.Requests.Count);
        }

        [Test]
        public void StopsRetryingAndThrowsAfterTheLastAttempt()
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(500, "error"));
            using var client = CreateClient(handler, maxRetries: 3);

            var exception = Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));

            Assert.AreEqual(500, exception!.StatusCode);
            Assert.AreEqual(4, handler.Requests.Count);
        }

        [Test]
        public void ExplainsAConnectionFailure()
        {
            var handler = new FakeThetaDataHandler(_ => throw new HttpRequestException("Connection refused"));
            using var client = CreateClient(handler, maxRetries: 1);

            var exception = Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));

            StringAssert.Contains("Theta Terminal", exception!.Message);
            StringAssert.Contains("host.docker.internal:25503", exception.Message);
            Assert.IsNull(exception.StatusCode);
            Assert.AreEqual(2, handler.Requests.Count);
        }

        [Test]
        public void RejectsABodyThatIsNotV3Json()
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(200, "symbol,expiration\n\"SPY\",\"2012-06-01\""));
            using var client = CreateClient(handler);

            Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));
        }

        [TestCase("\"No data\"")]
        [TestCase("42")]
        [TestCase("{\"response\":[{\"created\":\"N/A\",\"close\":1.5}]}")]
        [TestCase("{\"response\":[{\"created\":\"2026-10-20T17:15:44.979\",\"close\":\"abc\"}]}")]
        public void RejectsAJsonBodyThatHoldsNoV3Rows(string body)
        {
            var handler = new FakeThetaDataHandler(_ => FakeThetaDataHandler.Respond(200, body));
            using var client = CreateClient(handler);

            Assert.Throws<ThetaDataRequestException>(() => client.GetRows<EndOfDayRow>(ThetaDataEndpoints.EndOfDay(SecurityType.Equity), SpyEod));
        }

        [TestCase("1m", "20260101", "20260301", 3)]
        [TestCase("1h", "20260101", "20260128", 1)]
        [TestCase("1s", "20261019", "20261021", 3)]
        [TestCase("tick", "20261020", "20261020", 1)]
        public void SplitsTheDateRangeByTheV3Limits(string interval, string start, string end, int expectedRequests)
        {
            var parameters = new Dictionary<string, string>
            {
                [RequestParameters.Interval] = interval,
                [RequestParameters.StartDate] = start,
                [RequestParameters.EndDate] = end
            };

            var requests = ThetaDataRestApiClient.SplitByDateRange(parameters).ToList();

            Assert.AreEqual(expectedRequests, requests.Count);
            Assert.AreEqual(start, requests.First()[RequestParameters.StartDate]);
            Assert.AreEqual(end, requests.Last()[RequestParameters.EndDate]);
            for (var i = 1; i < requests.Count; i++)
            {
                var previousEnd = requests[i - 1][RequestParameters.EndDate].ConvertFromThetaDataDateFormat();
                Assert.AreEqual(previousEnd.AddDays(1), requests[i][RequestParameters.StartDate].ConvertFromThetaDataDateFormat(), "gap or overlap");
            }
        }

        [Test]
        public void DoesNotSplitAShortRequestWithoutAnInterval()
        {
            Assert.AreEqual(1, ThetaDataRestApiClient.SplitByDateRange(SpyEod).Count());
        }

        [Test]
        public void SplitsAnEndOfDayRequestIntoYears()
        {
            // The terminal answers more than 365 days with HTTP 400 "Too many days between start and end date; max 365 days allowed".
            var parameters = new Dictionary<string, string>
            {
                [RequestParameters.Symbol] = "SPY",
                [RequestParameters.StartDate] = "20230601",
                [RequestParameters.EndDate] = "20261026"
            };

            var requests = ThetaDataRestApiClient.SplitByDateRange(parameters).ToList();

            Assert.AreEqual(4, requests.Count);
            Assert.That(requests.All(x => (x[RequestParameters.EndDate].ConvertFromThetaDataDateFormat() - x[RequestParameters.StartDate].ConvertFromThetaDataDateFormat()).TotalDays < 365));
            Assert.AreEqual("20230601", requests[0][RequestParameters.StartDate]);
            Assert.AreEqual("20261026", requests[3][RequestParameters.EndDate]);
        }

        [Test]
        public void KeepsTheOrderOfTheRangesAndTheConcurrencyLimit()
        {
            var handler = new FakeThetaDataHandler(request => FakeThetaDataHandler.Respond(200,
                $"{{\"response\":[{{\"timestamp\":\"{request.Query["start_date"]!.ConvertFromThetaDataDateFormat():yyyy-MM-dd}T09:30:00.000\",\"open\":1,\"high\":1,\"low\":1,\"close\":1,\"volume\":1,\"count\":1}}]}}"))
            {
                Delay = TimeSpan.FromMilliseconds(30)
            };
            using var client = CreateClient(handler, maxConcurrentRequests: 2);
            var parameters = new Dictionary<string, string>
            {
                [RequestParameters.Symbol] = "SPY",
                [RequestParameters.Interval] = "1s",
                [RequestParameters.StartDate] = "20261001",
                [RequestParameters.EndDate] = "20261010"
            };

            var rows = client.GetRows<OhlcRow>(ThetaDataEndpoints.Ohlc(SecurityType.Equity), parameters);

            Assert.AreEqual(10, handler.Requests.Count);
            Assert.That(handler.MaxInFlight, Is.LessThanOrEqualTo(2));
            CollectionAssert.AreEqual(rows.Select(x => x.Timestamp).OrderBy(x => x).ToList(), rows.Select(x => x.Timestamp).ToList());
        }
    }
}
