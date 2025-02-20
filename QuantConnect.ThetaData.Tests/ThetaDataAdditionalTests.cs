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
using NUnit.Framework;
using System.Collections.Generic;

namespace QuantConnect.Lean.DataSource.ThetaData.Tests;

[TestFixture]
public class ThetaDataAdditionalTests
{
    [Test]
    public void GenerateDateRangesWithNinetyDaysInterval()
    {
        var intervalDays = 90;
        var startDate = new DateTime(2020, 07, 18);
        var endDate = new DateTime(2021, 01, 14);

        var expectedRanges = new List<(DateTime startDate, DateTime endDate)>
        {
            (new DateTime(2020, 07, 18), new DateTime(2020, 10, 16)),
            (new DateTime(2020, 10, 17), new DateTime(2021, 01, 14)),
        };

        var actualRanges = new List<(DateTime startDate, DateTime endDate)>(ThetaDataExtensions.GenerateDateRangesWithInterval(startDate, endDate, intervalDays));

        Assert.AreEqual(expectedRanges.Count, actualRanges.Count, "The number of ranges should match.");

        for (int i = 0; i < expectedRanges.Count; i++)
        {
            Assert.AreEqual(expectedRanges[i].startDate, actualRanges[i].startDate, $"Start date mismatch at index {i}");
            Assert.AreEqual(expectedRanges[i].endDate, actualRanges[i].endDate, $"End date mismatch at index {i}");
        }
    }

    [Test]
    public void GenerateDateRangesWithOneDayInterval()
    {
        var intervalDays = 1;

        var startDate = new DateTime(2024, 07, 26);
        var endDate = new DateTime(2024, 07, 30);

        var expectedRanges = new List<(DateTime startDate, DateTime endDate)>
        {
            (new DateTime(2024, 07, 26), new DateTime(2024, 07, 27)),
            (new DateTime(2024, 07, 28), new DateTime(2024, 07, 29)),
            (new DateTime(2024, 07, 30), new DateTime(2024, 07, 30))
        };

        var actualRanges = new List<(DateTime startDate, DateTime endDate)>(ThetaDataExtensions.GenerateDateRangesWithInterval(startDate, endDate, intervalDays));

        Assert.AreEqual(expectedRanges.Count, actualRanges.Count, "The number of ranges should match.");

        for (int i = 0; i < expectedRanges.Count; i++)
        {
            Assert.AreEqual(expectedRanges[i].startDate, actualRanges[i].startDate, $"Start date mismatch at index {i}");
            Assert.AreEqual(expectedRanges[i].endDate, actualRanges[i].endDate, $"End date mismatch at index {i}");
        }
    }

    [Test]
    public void GenerateDateRangesWithInterval_ShouldHandleSameStartEndDate()
    {
        DateTime startDate = new DateTime(2025, 2, 1);
        DateTime endDate = new DateTime(2025, 2, 1);

        var ranges = new List<(DateTime startDate, DateTime endDate)>(
            ThetaDataExtensions.GenerateDateRangesWithInterval(startDate, endDate, 1)
        );

        Assert.AreEqual(1, ranges.Count, "There should be no date ranges generated.");
    }
}
