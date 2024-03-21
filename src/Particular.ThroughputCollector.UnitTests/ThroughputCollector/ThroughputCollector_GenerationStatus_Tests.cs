namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_GenerationStatus_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_no_throughput_for_last_30_days()
    {
        EndpointsWithNoThroughputInLast30Days.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_true_when_there_is_throughput_for_last_30_days()
    {
        EndpointsWithThroughputInLast30Days.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.True);
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_throughput_exists_only_for_today()
    {
        EndpointsWithThroughputOnlyForToday.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    }

    readonly List<Endpoint> EndpointsWithNoThroughputInLast30Days =
    [
        new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-31), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-32), TotalThroughput = 50 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-31), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-32), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputInLast30Days =
    [
        new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputOnlyForToday =
    [
        new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow), TotalThroughput = 50 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow), TotalThroughput = 50 }] }
    ];
}