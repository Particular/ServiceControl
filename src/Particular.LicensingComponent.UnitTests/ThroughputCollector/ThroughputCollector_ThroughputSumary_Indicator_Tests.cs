namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_ThroughputSumary_Indicator_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {

        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_indicate_known_endpoint_if_at_least_one_instance_of_it_exists_in_the_sources()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Audit])
                .WithThroughput(data: [50, 75])
            .AddEndpoint("Endpoint1_", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint =>
            {
                endpoint.SanitizedName = "Endpoint1";
                endpoint.EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()];
            })
                .WithThroughput(data: [60, 65])
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(1));

        Assert.That(summary[0].IsKnownEndpoint, Is.True, $"Incorrect IsKnownEndpoint recorded for {summary[0].Name}");
    }

    [Test]
    public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
    {
        // Arrange
        var userIndicator = "SomeIndicator";
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Monitoring]).WithThroughput(days: 2)
            .Build();
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Act
        List<UpdateUserIndicator> endpointsWithUpdates = [new UpdateUserIndicator() { Name = "Endpoint1", UserIndicator = userIndicator }];
        await ThroughputCollector.UpdateUserIndicatorsOnEndpoints(endpointsWithUpdates, default);

        // Assert
        var updatedEndpoints = await DataStore.GetAllEndpoints(true, default);
        Assert.That(updatedEndpoints, Is.Not.Null);
        Assert.That(updatedEndpoints.Count, Is.EqualTo(2));

        Assert.That(updatedEndpoints.All(a => a.UserIndicator == userIndicator), Is.True, $"Incorrect UserIndicator recorded for Endpoint1");
    }
}