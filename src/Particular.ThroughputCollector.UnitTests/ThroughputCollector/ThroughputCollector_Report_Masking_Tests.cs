namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Masking_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_mask_endpoint_names_when_mask_provided()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(["Endpoint1"], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Null, $"QueueName not masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "REDACTED1"), Is.Not.Null, $"QueueName not masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
    }

    [Test]
    public async Task Should_not_mask_endpoint_names_when_no_mask_provided()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName.Contains("REDACTED")), Is.Null, $"QueueNames should not be masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Not.Null, $"QueueName Endpoint1 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
    }

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerOnly =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];
}