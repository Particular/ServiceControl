namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Dates_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_return_correct_dates_for_report_when_multiple_sources_with_different_dates()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAuditWithDifferentDates.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        var minDateInReport = new DateTimeOffset(DateTime.UtcNow.AddDays(-5).Date, TimeSpan.Zero);
        var reportEndDate = new DateTimeOffset(DateTime.UtcNow.AddDays(-1).Date, TimeSpan.Zero);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.StartTime, Is.EqualTo(minDateInReport), $"Incorrect StartTime for report");
        Assert.That(report.ReportData.EndTime, Is.EqualTo(reportEndDate), $"Incorrect StartTime for report");
        Assert.That(report.ReportData.ReportDuration, Is.EqualTo(reportEndDate - minDateInReport), $"Incorrect ReportDuration for report");
    }

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoringAndAuditWithDifferentDates =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-4), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 61 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 64 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 57 }] },
        new Endpoint("Endpoint3", ThroughputSource.Monitoring) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5), TotalThroughput = 45 }] },
        new Endpoint("Endpoint3", ThroughputSource.Audit) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 42 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 47 }] }
    ];
}