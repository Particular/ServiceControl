namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Prefix_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_not_include_endpoints_matching_prefix_in_name_if_prefix_provided_single_throughput_source()
    {
        EndpointsWithPrefixInNameFromOneSource.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var prefix = "Endpoint";
        var report = await ThroughputCollector.GenerateThroughputReport(prefix, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(2), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.IgnoredQueues.Length, Is.EqualTo(1), $"Invalid IgnoredQueues on report");
        Assert.That(report.ReportData.IgnoredQueues.Contains("SomeOtherQueue"), Is.True, $"SomeOtherQueue should not be on report");
        Assert.That(report.ReportData.Queues.Any(w => !w.QueueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)), Is.False, $"Only queues matching prefix should be included in report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Not.Null, $"QueueName Endpoint1 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "endpoint2"), Is.Not.Null, $"QueueName endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "SomeOtherQueue"), Is.Null, $"SomeOtherQueue should not be on report");
    }

    [Test]
    public async Task Should_not_include_endpoints_matching_prefix_in_name_if_prefix_provided_multiple_throughput_source()
    {
        EndpointsWithPrefixInNameFromMultipleSources.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var prefix = "Endpoint";
        var report = await ThroughputCollector.GenerateThroughputReport(prefix, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.IgnoredQueues.Length, Is.EqualTo(1), $"Invalid IgnoredQueues on report");
        Assert.That(report.ReportData.IgnoredQueues.Contains("SomeOtherQueue"), Is.True, $"SomeOtherQueue should not be on report");
        Assert.That(report.ReportData.Queues.Any(w => !w.QueueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)), Is.False, $"Only queues matching prefix should be included in report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "endpoint1_"), Is.Not.Null, $"QueueName endpoint1_ not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "SomeOtherQueue"), Is.Null, $"SomeOtherQueue should not be on report");
    }

    [Test]
    public async Task Should_include_all_endpoints_if_no_prefix_provided()
    {
        EndpointsWithPrefixInNameFromOneSource.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, [], null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.IgnoredQueues.Length, Is.EqualTo(0), $"Invalid IgnoredQueues on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Not.Null, $"QueueName Endpoint1 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "endpoint2"), Is.Not.Null, $"QueueName endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "SomeOtherQueue"), Is.Not.Null, $"QueueName SomeOtherQueue not found on report");
    }

    readonly List<Endpoint> EndpointsWithPrefixInNameFromOneSource =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("endpoint2", ThroughputSource.Broker) { SanitizedName = "endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("SomeOtherQueue", ThroughputSource.Broker) { SanitizedName = "SomeOtherQueue", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithPrefixInNameFromMultipleSources =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("endpoint1_", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("SomeOtherQueue", ThroughputSource.Broker) { SanitizedName = "SomeOtherQueue", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];
}