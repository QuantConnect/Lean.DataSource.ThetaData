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

using System.Diagnostics;
using QuantConnect.Logging;
using QuantConnect.Lean.DataSource.ThetaData.Models.Common;

namespace QuantConnect.Lean.DataSource.ThetaData.Models.Wrappers;

/// <summary>
/// A utility class that conditionally starts a stopwatch for measuring execution time 
/// when debugging is enabled. Implements <see cref="IDisposable"/> to ensure 
/// automatic logging upon completion.
/// </summary>
public class StopwatchWrapper : IDisposable
{
    private readonly Stopwatch? _stopwatch;
    private readonly string _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="StopwatchWrapper"/> class
    /// and starts a stopwatch to measure execution time.
    /// </summary>
    /// <param name="message">A descriptive message to include in the log output.</param>
    private StopwatchWrapper(string message)
    {
        _message = message;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Starts a stopwatch if debugging is enabled and returns an appropriate disposable instance.
    /// </summary>
    /// <param name="message">A descriptive message to include in the log output.</param>
    /// <returns>
    /// A <see cref="StopwatchWrapper"/> instance if debugging is enabled, 
    /// otherwise a no-op <see cref="NullDisposable"/> instance.
    /// </returns>
    public static IDisposable? StartIfEnabled(string message)
    {
        return Log.DebuggingEnabled ? new StopwatchWrapper(message) : null;
    }

    /// <summary>
    /// Stops the stopwatch and logs the elapsed time if debugging is enabled.
    /// </summary>
    public void Dispose()
    {
        if (_stopwatch != null)
        {
            _stopwatch.Stop();
            Log.Debug($"{_message} completed in {_stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
