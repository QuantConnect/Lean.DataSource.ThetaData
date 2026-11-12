# Migration from ThetaData API v2 to v3

This connector now speaks the ThetaData REST API v3 only. Theta Terminal v3
answers every v2 request with HTTP 410 and the text "We have upgraded to API
v3" (checked on 2026-10-28 against a local terminal). So a v2 connector cannot
fetch any data, and a connector that supports both versions has no v2 server
to talk to.

## Selected architecture

The connector stays a native C# LEAN plugin. It sends HTTP requests to Theta
Terminal v3, which runs on the same machine or network and holds the ThetaData
login.

- `ThetaDataRestApiClient` is the only class that sends HTTP requests. It takes
  an `HttpMessageHandler`, so the tests replace the network with recorded
  responses.
- `ThetaDataEndpoints` builds every v3 path. `RequestParameters` holds every v3
  query parameter name.
- `Models/Rest/ThetaDataRows.cs` holds 1 typed class for each v3 response row.
- `ThetaDataBarConverter` turns rows into LEAN data. It is the only place that
  decides which rows hold no market observation.

2 alternatives were rejected:

- **v2 and v3 side by side:** ThetaData retired v2, so the v2 half can never be
  tested or used.
- **The ThetaData Python library:** it connects over gRPC without the terminal
  (https://thetadata.net/docs/Python-Library/Getting-Started.html). LEAN plugins
  are .NET assemblies, so the library would need a Python process next to the
  engine, and a second way to fail. It is useful as an independent source to
  cross-check data, not as the transport.

## What changed in v3

| Item | v2 | v3 | Source |
| --- | --- | --- | --- |
| Base address | `http://127.0.0.1:25510/v2/` | `http://127.0.0.1:25503/v3/` | OpenAPI `servers` entry; the terminal |
| Paths | `hist/option/eod`, `list/contracts/option/quote` | `option/history/eod`, `option/list/contracts/quote` | OpenAPI spec |
| Root parameter | `root` | `symbol` | OpenAPI spec |
| Expiration | `exp=20240119` | `expiration=20240119`, or `*` for every expiration | OpenAPI spec |
| Strike | `strike=470000` (tenths of a cent) | `strike=470.000` (dollars), or `*` | OpenAPI spec |
| Right | `right=C` / `P` | `right=call` / `put` / `both` | OpenAPI spec |
| Interval | `ivl=60000` (milliseconds) | `interval=1m` (`tick`, `1s`, `1m`, `1h`, and others) | OpenAPI spec |
| Time of day | `start_time=0` (ms of day) | `start_time=09:30:00`, default 09:30 to 16:00 ET | OpenAPI spec |
| Response | JSON with a `header` object and positional arrays | CSV by default; JSON with `format=json`, named fields | the terminal |
| Option rows | 1 contract per request | `{"contract": {...}, "data": [...]}`, many contracts per request | the terminal |
| Timestamps | `ms_of_day` plus `date` as integers | ISO text, for example `2026-10-20T17:15:44.979`, in ET | the terminal |
| Pagination | `header.next_page` | none seen; no `next_page` in the spec or the response headers | the terminal, 5 MB response |
| No data | HTTP 472 | HTTP 472, "No data found for your request" | the terminal |
| Plan denies data | not handled by the old code: retried, then a generic exception | HTTP 403 with a text body. The error-code page also lists 471. | the terminal; v3 "Error Codes" page |
| Date range | split by the client | intervals under 1 minute: 1 day per request; 1 minute and more: at most 1 month; no interval (end-of-day): at most 365 days, else HTTP 400 | v3 docs, "Request Sizing"; the terminal |

The streaming API did not change. The v3 docs still name
`ws://127.0.0.1:25520/v1/events`, with strikes in tenths of a cent and rights
"C"/"P".

## Configuration keys

| Key | Before | Now |
| --- | --- | --- |
| `thetadata-rest-url` | default `http://127.0.0.1:25510`; `/v2` added by the code | default `http://127.0.0.1:25503`; `/v3` added by the code. A trailing `/v3` is accepted. A value that ends in `/v2` is rejected with an error that names the fix. Port 25510 logs a warning. |
| `thetadata-subscription-plan` | `Free`, `Value`, `Standard`, `Pro` | unchanged. The downloader defaults to `Free` when it is empty. |
| `thetadata-ws-url` | `ws://127.0.0.1:25520/v1/events` | unchanged |
| `thetadata-max-concurrent-requests` | none | new. Default by plan: Free 1, Value 2, Standard 4, Pro 8 (v3 docs, "Concurrent Requests"). |
| `thetadata-request-timeout-seconds` | none, 5 minutes | new, default 300 |

**Caution:** the lean CLI's module list gives `thetadata-rest-url` the default
`http://host.docker.internal:25510`. That is the v2 port. Pass
`--thetadata-rest-url http://host.docker.internal:25503` on every `lean`
command, or the requests fail to connect.

## Required versions

- Theta Terminal v3 (`ThetaTerminalv3.jar`), which needs Java 21 or later
  (https://thetadata.net/docs/Articles/Getting-Started/Getting-Started.html).
  The tests ran against the terminal on port 25503 on 2026-10-28. The terminal
  build number was not recorded.
- .NET SDK 10. The projects target `net10.0`.
- LEAN: the connector compiles against the NuGet package
  `QuantConnect.Lean.Engine` 2.5.18208. The CLI runs used an engine image built
  from LEAN master at `d4cac3751` (2026-10-16), plus 1 local LEAN commit about
  canonical downloads that this change does not rely on.

## Breaking changes

- A v2 terminal or a v2 address no longer works.
- `ThetaDataSymbolMapper.GetLeanSymbol(ticker, securityType, market, optionStyle, expiration, strike, ...)`
  now takes the strike in dollars. The streaming overload, which takes a
  `ContractSecurityType`, still takes tenths of a cent.
- `ThetaDataRestApiClient.ExecuteRequest<T>` is replaced by `GetRows<T>`. The
  v2 models and converters (`BaseResponse`, `EndOfDayReportResponse`,
  `QuoteListContract`, and the others) are removed.
- `ThetaDataExtensions.GenerateDateRangesWithInterval(start, end, n)` now
  returns ranges of exactly `n` days. Before, each range held `n + 1` days.
- A request that the plan denies returns null and logs 1 error. Before, it
  retried 2 times and then threw a generic exception.

## Features that need a paid plan

Observed on the Free plan on 2026-10-28:

| Request | Free plan |
| --- | --- |
| Stock, option, and index end-of-day history from 2023-06-01 | HTTP 200 |
| Option end-of-day history with `expiration=*` (the whole chain of 1 day) | HTTP 200, 5 MB and about 20 s for SPY |
| Option expirations and strikes | HTTP 200 |
| Stock end-of-day history before 2023-06-01 | HTTP 403 |
| `option/list/contracts/*` | HTTP 403. The connector then lists the chain from the end-of-day report. |
| `option/history/open_interest` | HTTP 403 |
| `option/history/quote`, `stock/history/ohlc` (intraday) | HTTP 403 |
| Streaming on port 25520 | no listener |

The v3 docs say the Free plan has no index data. The terminal still returned
SPX end-of-day data on 2026-10-28.

So the intraday, tick, open interest, and streaming paths of the connector are
built against the v3 spec but are not tested against a terminal.

## Roll back

The v2 connector cannot run against Theta Terminal v3. To go back anyway:

1. Check out the commit before this change, `d9e4e04` on `master`.
2. Run a Theta Terminal that still serves v2 on port 25510, if ThetaData still
   provides one.
3. Rebuild the connector and the engine image.
