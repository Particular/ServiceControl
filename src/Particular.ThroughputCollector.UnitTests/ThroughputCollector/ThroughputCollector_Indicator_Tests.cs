namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Indicator_Tests : ThroughputCollectorTestFixture
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

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(1));

        Assert.That(summary[0].IsKnownEndpoint, Is.EqualTo(true), $"Incorrect IsKnownEndpoint recorded for {summary[0].Name}");
    }

    [Test]
    public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
    {
        EndpointsWithNoUserIndicatorsFromMultipleSources.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(1));

        Assert.That(summary[0].UserIndicator, Is.EqualTo(string.Empty));

        var userIndicator = "SomeIndicator";
        List<EndpointThroughputSummary> endpointsWithUpdates = [new EndpointThroughputSummary { Name = "Endpoint1", UserIndicator = userIndicator }];
        await ThroughputCollector.UpdateUserIndicatorsOnEndpoints(endpointsWithUpdates);

        var updatedEndpoints = await DataStore.GetAllEndpoints();
        Assert.That(updatedEndpoints, Is.Not.Null);
        Assert.That(updatedEndpoints.Count, Is.EqualTo(2));

        Assert.That(updatedEndpoints.All(a => a.UserIndicator == userIndicator), Is.True, $"Incorrect UserIndicator recorded for Endpoint1");
    }

    readonly List<Endpoint> EndpointsWithNoUserIndicatorsFromMultipleSources =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];

    readonly List<Endpoint> EndpointsWithMultipleSourcesAndEndpointIndicator =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1_", ThroughputSource.Audit) { SanitizedName = "Endpoint1", EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()], DailyThroughput = [new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];
}