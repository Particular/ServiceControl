namespace Particular.ThroughputCollector.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;

[TestFixture]
class EndpointsTests : PersistenceTestFixture
{
    [Test]
    public async Task Should_add_new_endpoint_when_no_endpoints()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);

        // Act
        await DataStore.SaveEndpoint(endpoint);

        // Assert
        var endpoints = await DataStore.GetAllEndpoints();
        var foundEndpoint = endpoints.Single();

        Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint.Id.Name));
        Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint.Id.ThroughputSource));
    }

    [Test]
    public async Task Should_add_new_endpoint_when_name_is_the_same_but_source_different()
    {
        // Arrange
        var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit);
        var endpoint2 = new Endpoint("Endpoint1", ThroughputSource.Broker);

        // Act
        await DataStore.SaveEndpoint(endpoint1);
        await DataStore.SaveEndpoint(endpoint2);

        // Assert
        var endpoints = await DataStore.GetAllEndpoints();

        Assert.That(endpoints.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_update_endpoint_that_already_has_throughput_with_new_throughput()
    {
        // Arrange
        var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1" };
        await DataStore.SaveEndpoint(endpoint1);
        await DataStore.RecordEndpointThroughput(endpoint1.Id.Name, ThroughputSource.Audit, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), 50);

        // Act
        await DataStore.RecordEndpointThroughput(endpoint1.Id.Name, ThroughputSource.Audit, DateOnly.FromDateTime(DateTime.UtcNow), 100);

        // Assert
        var endpoints = await DataStore.GetAllEndpoints();
        var throughput = await DataStore.GetEndpointThroughputByQueueName([endpoint1.SanitizedName]);

        var foundEndpoint = endpoints.Single();
        Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint1.Id.Name));
        Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint1.Id.ThroughputSource));
        Assert.That(throughput, Contains.Key(endpoint1.SanitizedName));
        Assert.That(throughput[endpoint1.SanitizedName].Count, Is.EqualTo(1), "Should be only a single ThroughputData returned");
        Assert.That(throughput[endpoint1.SanitizedName].Single().Count, Is.EqualTo(2), "Should be two days of throughput data");
    }

    [Test]
    public async Task Should_retrieve_matching_endpoint_when_same_source()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);
        await DataStore.SaveEndpoint(endpoint);

        // Act
        var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null);
    }

    [Test]
    public async Task Should_not_retrieve_matching_endpoint_when_different_source()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);
        await DataStore.SaveEndpoint(endpoint);

        // Act
        var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Broker);

        // Assert
        Assert.That(foundEndpoint, Is.Null);
    }

    [Test]
    public async Task Should_update_user_indicators_and_nothing_else()
    {
        // Arrange
        var userIndicator = "someIndicator";

        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit)
        {
            SanitizedName = "Endpoint"
        };
        await DataStore.SaveEndpoint(endpoint);

        // Act
        await DataStore.UpdateUserIndicatorOnEndpoints([new Endpoint("Endpoint") { SanitizedName = "Endpoint", UserIndicator = userIndicator }]);

        // Assert
        var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

        Assert.That(foundEndpoint, Is.Not.Null);
        Assert.That(foundEndpoint.UserIndicator, Is.EqualTo(userIndicator));
    }

    [Test]
    public async Task Should_not_add_endpoint_when_updating_user_indication()
    {
        // Arrange
        var endpointWithUserIndicators = new Endpoint("Endpoint", ThroughputSource.Audit)
        {
            SanitizedName = "Endpoint",
            UserIndicator = "someIndicator",
        };

        // Act
        await DataStore.UpdateUserIndicatorOnEndpoints([endpointWithUserIndicators]);

        // Assert
        var foundEndpoint = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
        var allEndpoints = await DataStore.GetAllEndpoints();

        Assert.That(foundEndpoint, Is.Null);
        Assert.That(allEndpoints.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Should_update_indicators_on_all_endpoint_sources()
    {
        // Arrange
        var userIndicator = "someIndicator";

        var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit) { SanitizedName = "Endpoint" };
        var endpointMonitoring = new Endpoint("Endpoint", ThroughputSource.Monitoring) { SanitizedName = "Endpoint" };

        await DataStore.SaveEndpoint(endpointAudit);
        await DataStore.SaveEndpoint(endpointMonitoring);

        // Act
        await DataStore.UpdateUserIndicatorOnEndpoints([new Endpoint("Endpoint") { SanitizedName = "Endpoint", UserIndicator = userIndicator }]);

        // Assert
        var foundEndpointAudit = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);
        var foundEndpointMonitoring = await DataStore.GetEndpoint("Endpoint", ThroughputSource.Monitoring);

        Assert.That(foundEndpointAudit, Is.Not.Null);
        Assert.That(foundEndpointAudit.UserIndicator, Is.EqualTo(userIndicator));

        Assert.That(foundEndpointMonitoring, Is.Not.Null);
        Assert.That(foundEndpointMonitoring.UserIndicator, Is.EqualTo(userIndicator));
    }

    [TestCase(10, 5, false)]
    [TestCase(10, 20, true)]
    public async Task Should_correctly_report_throughput_existence_for_X_days(int daysSinceLastThroughputEntry, int timeFrameToCheck, bool expectedValue)
    {
        // Arrange
        var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit);
        await DataStore.SaveEndpoint(endpointAudit);

        await DataStore.RecordEndpointThroughput(
            endpointAudit.Id.Name,
            ThroughputSource.Audit,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysSinceLastThroughputEntry),
            50);

        Assert.That(await DataStore.IsThereThroughputForLastXDays(timeFrameToCheck), expectedValue ? Is.True : Is.False);
    }
}