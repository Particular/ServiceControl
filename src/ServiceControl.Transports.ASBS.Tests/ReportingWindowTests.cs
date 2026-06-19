namespace ServiceControl.Transports.ASBS.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class ReportingWindowTests
{
    [TestCase(90, 30)]
    [TestCase(87, 30)]
    [TestCase(100, 30)]
    [TestCase(1, 30)]
    public void CanHandle90Days(int totalDays, int reportingWindowDays)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var startDate = now.AddDays(-totalDays);

        List<DateOnly> expectedCoveredDates = [];
        for (var currentDate = startDate; currentDate <= now; currentDate = currentDate.AddDays(1))
        {
            expectedCoveredDates.Add(currentDate);
        }

        var reportingWindow = ReportingWindow.GetReportingWindow(startDate, now, reportingWindowDays).ToArray();

        using (Assert.EnterMultipleScope())
        {
            List<DateOnly> coveredDates = [];

            foreach (var window in reportingWindow)
            {
                // Everything in the reporting window must be greater than zero days and less than or equal to reportingWindowDays
                var numberOfDays = (window.End.ToDateTime(TimeOnly.MinValue) - window.Start.ToDateTime(TimeOnly.MaxValue)).TotalDays;
                Assert.That(numberOfDays, Is.LessThanOrEqualTo(reportingWindowDays), $"Window from {window.Start} to {window.End} has {numberOfDays} days, which exceeds the reporting window of {reportingWindowDays} days.");
                Assert.That(numberOfDays, Is.GreaterThan(0), $"Window from {window.Start} to {window.End} has {numberOfDays} days, which is not greater than zero.");

                // None of the reportingWindows start or end after the endDate
                Assert.That(window.Start, Is.LessThanOrEqualTo(now), $"Window from {window.Start} to {window.End} starts after the end date.");
                Assert.That(window.End, Is.LessThanOrEqualTo(now), $"Window from {window.Start} to {window.End} ends after the end date.");

                // None of the reportingWindows start or end before the startDate
                Assert.That(window.Start, Is.GreaterThanOrEqualTo(startDate), $"Window from {window.Start} to {window.End} starts before the start date.");
                Assert.That(window.End, Is.GreaterThanOrEqualTo(startDate), $"Window from {window.Start} to {window.End} ends before the start date.");

                for (var currentDate = window.Start; currentDate <= window.End; currentDate = currentDate.AddDays(1))
                {
                    coveredDates.Add(currentDate);
                }
            }

            // None of the reportingWindows overlap
            // There are no gaps
            Assert.That(coveredDates, Is.EquivalentTo(expectedCoveredDates), "The covered dates do not match the expected dates.");
        }
    }
}