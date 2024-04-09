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

using NodaTime;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Securities;
using System.Collections.Concurrent;

namespace QuantConnect.Lean.DataSource.ThetaData
{
    public class ThetaDataDownloader : IDataDownloader, IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly ThetaDataProvider _historyProvider;

        /// <inheritdoc cref="MarketHoursDatabase" />
        private readonly MarketHoursDatabase _marketHoursDatabase;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThetaDataDownloader"/>
        /// </summary>
        public ThetaDataDownloader()
        {
            _historyProvider = new();
            _marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        }

        public IEnumerable<BaseData>? Get(DataDownloaderGetParameters downloadParameters)
        {
            var symbol = downloadParameters.Symbol;

            var dataType = LeanData.GetDataType(downloadParameters.Resolution, downloadParameters.TickType);
            var exchangeHours = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);
            var dataTimeZone = _marketHoursDatabase.GetDataTimeZone(symbol.ID.Market, symbol, symbol.SecurityType);

            if (symbol.IsCanonical())
            {
                return GetCanonicalOptionHistory(
                    symbol,
                    downloadParameters.StartUtc,
                    downloadParameters.EndUtc,
                    dataType,
                    downloadParameters.Resolution,
                    exchangeHours,
                    dataTimeZone,
                    downloadParameters.TickType);
            }
            else
            {
                var historyRequest = new HistoryRequest(
                    startTimeUtc: downloadParameters.StartUtc,
                    endTimeUtc: downloadParameters.EndUtc, dataType,
                    symbol: symbol,
                    resolution: downloadParameters.Resolution,
                    exchangeHours: exchangeHours,
                    dataTimeZone: dataTimeZone,
                    fillForwardResolution: downloadParameters.Resolution,
                    includeExtendedMarketHours: true,
                    isCustomData: false,
                    dataNormalizationMode: DataNormalizationMode.Raw,
                    tickType: downloadParameters.TickType);

                var historyData = _historyProvider.GetHistory(historyRequest);

                if (historyData == null)
                {
                    return null;
                }

                return historyData;
            }
        }

        private IEnumerable<BaseData>? GetCanonicalOptionHistory(Symbol symbol, DateTime startUtc, DateTime endUtc, Type dataType,
            Resolution resolution, SecurityExchangeHours exchangeHours, DateTimeZone dataTimeZone, TickType tickType)
        {
            var blockingOptionCollection = new BlockingCollection<BaseData>();
            var symbols = GetOptions(symbol, startUtc, endUtc);

            // Symbol can have a lot of Option parameters
            Task.Run(() => Parallel.ForEach(symbols, targetSymbol =>
            {
                var historyRequest = new HistoryRequest(startUtc, endUtc, dataType, targetSymbol, resolution, exchangeHours, dataTimeZone,
                    resolution, true, false, DataNormalizationMode.Raw, tickType);

                var history = _historyProvider.GetHistory(historyRequest);

                // If history is null, it indicates an incorrect or missing request for historical data,
                // so we skip processing for this symbol and move to the next one.
                if (history == null)
                {
                    return;
                }

                foreach (var data in history)
                {
                    blockingOptionCollection.Add(data);
                }
            })).ContinueWith(_ =>
            {
                blockingOptionCollection.CompleteAdding();
            });

            var options = blockingOptionCollection.GetConsumingEnumerable();

            // Validate if the collection contains at least one successful response from history.
            if (!options.Any())
            {
                return null;
            }

            return options;
        }

        protected virtual IEnumerable<Symbol> GetOptions(Symbol symbol, DateTime startUtc, DateTime endUtc)
        {
            var exchangeHours = _marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);

            return Time.EachTradeableDay(exchangeHours, startUtc.Date, endUtc.Date)
                .Select(date => _historyProvider.GetOptionChain(symbol, date))
                .SelectMany(x => x)
                .Distinct();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _historyProvider.DisposeSafely();
        }
    }
}
