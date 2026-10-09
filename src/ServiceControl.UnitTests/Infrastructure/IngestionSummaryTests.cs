namespace ServiceControl.UnitTests.Infrastructure;

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Infrastructure.Ingestion;

[TestFixture]
public class IngestionSummaryTests
{
    static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Reports_only_uptime_during_the_first_hour()
    {
        var snapshot = new IngestionCountersSnapshot(Start, 500, 100, 10, 5, 2, 1, 50);

        var data = Describe(snapshot, Start.AddMinutes(59));

        Assert.That(data, Is.EqualTo(new Dictionary<string, string> { ["Health.UptimeHours"] = "0" }));
    }

    [Test]
    public void Rates_are_per_day_and_percentages_are_of_uptime()
    {
        var snapshot = new IngestionCountersSnapshot(Start, 4800, 43200, 48, 100, 50, 10, 1000);

        var data = Describe(snapshot, Start.AddDays(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data, Does.ContainKey("Health.UptimeHours").WithValue("48"));
            Assert.That(data, Does.ContainKey("Ingestion.AvgDailyMessages").WithValue("2400"));
            Assert.That(data, Does.ContainKey("Ingestion.BusyPercent").WithValue("25"));
            Assert.That(data, Does.ContainKey("Ingestion.StorageMsPerMessage").WithValue("10"));
            Assert.That(data, Does.ContainKey("Health.LagOver1MinPercent").WithValue("10"));
            Assert.That(data, Does.ContainKey("Health.LagOver10MinPercent").WithValue("5"));
            Assert.That(data, Does.ContainKey("Health.LagOver60MinPercent").WithValue("1"));
        }
    }

    [Test]
    public void Busy_percent_is_capped_when_batches_overlap()
    {
        var snapshot = new IngestionCountersSnapshot(Start, 10, 7200, 0, 0, 0, 0, 0);

        var data = Describe(snapshot, Start.AddHours(1));

        Assert.That(data, Does.ContainKey("Ingestion.BusyPercent").WithValue("100"));
    }

    [Test]
    public void Lag_is_left_out_until_a_message_carried_the_header()
    {
        var snapshot = new IngestionCountersSnapshot(Start, 10, 1, 0, 0, 0, 0, 0);

        var data = Describe(snapshot, Start.AddHours(1));

        Assert.That(data.Keys, Has.None.StartsWith("Health.LagOver"));
    }

    [Test]
    public void Storage_time_is_left_out_when_not_asked_for_or_nothing_was_ingested()
    {
        var withoutStorage = new IngestionCountersSnapshot(Start, 10, 1, 1, 0, 0, 0, 0);
        var withoutMessages = new IngestionCountersSnapshot(Start, 0, 1, 1, 0, 0, 0, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Describe(withoutStorage, Start.AddHours(1), includeStorage: false), Does.Not.ContainKey("Ingestion.StorageMsPerMessage"));
            Assert.That(Describe(withoutMessages, Start.AddHours(1)), Does.Not.ContainKey("Ingestion.StorageMsPerMessage"));
        }
    }

    static Dictionary<string, string> Describe(IngestionCountersSnapshot snapshot, DateTime nowUtc, bool includeStorage = true) =>
        IngestionSummary.Describe(snapshot, nowUtc, "Ingestion", "Health", includeStorage).ToDictionary(pair => pair.Key, pair => pair.Value);
}
