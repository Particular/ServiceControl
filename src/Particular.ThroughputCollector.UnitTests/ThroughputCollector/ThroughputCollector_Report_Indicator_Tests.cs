namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Indicator_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_indicate_known_endpoint_if_at_least_one_instance_of_it_exists_in_the_sources()
    {
        EndpointsWithMultipleSourcesAndEndpointIndicator.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Length, Is.EqualTo(1));

        Assert.That(report.ReportData.Queues[0].EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()), Is.True, $"Incorrect IsKnownEndpoint recorded for {report.ReportData.Queues[0].QueueName}");
    }

    [Test]
    public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
    {
        EndpointsWithNoUserIndicatorsFromMultipleSources.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Length, Is.EqualTo(1));

        Assert.That(report.ReportData.Queues[0].UserIndicator, Is.EqualTo(UserIndicator.NServicebusEndpointScaledOut.ToString()));
    }

    readonly List<Endpoint> EndpointsWithNoUserIndicatorsFromMultipleSources =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", UserIndicator = UserIndicator.NServicebusEndpointScaledOut.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];

    readonly List<Endpoint> EndpointsWithMultipleSourcesAndEndpointIndicator =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1_", ThroughputSource.Audit) { SanitizedName = "Endpoint1", EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()], DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];
}