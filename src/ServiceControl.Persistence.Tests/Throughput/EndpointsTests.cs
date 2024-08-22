namespace ServiceControl.Persistence.Tests.Throughput;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

[TestFixture]
class EndpointsTests : PersistenceTestBase
{
    [Test]
    public async Task Should_add_new_endpoint_when_no_endpoints()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);

        // Act
        await LicensingDataStore.SaveEndpoint(endpoint, default);

        // Assert
        var endpoints = await LicensingDataStore.GetAllEndpoints(true, default);
        var foundEndpoint = endpoints.Single();

        Assert.Multiple(() =>
        {
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint.Id.Name));
            Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint.Id.ThroughputSource));
        });
    }

    [Test]
    public async Task Should_add_new_endpoint_when_name_is_the_same_but_source_different()
    {
        // Arrange
        var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit);
        var endpoint2 = new Endpoint("Endpoint1", ThroughputSource.Broker);

        // Act
        await LicensingDataStore.SaveEndpoint(endpoint1, default);
        await LicensingDataStore.SaveEndpoint(endpoint2, default);

        // Assert
        var endpoints = await LicensingDataStore.GetAllEndpoints(true, default);

        Assert.That(endpoints.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Should_update_endpoint_that_already_has_throughput_with_new_throughput()
    {
        // Arrange
        var endpoint1 = new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1" };
        await LicensingDataStore.SaveEndpoint(endpoint1, default);
        await LicensingDataStore.RecordEndpointThroughput(endpoint1.Id.Name, ThroughputSource.Audit, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), 50, default);

        // Act
        await LicensingDataStore.RecordEndpointThroughput(endpoint1.Id.Name, ThroughputSource.Audit, DateOnly.FromDateTime(DateTime.UtcNow), 100, default);

        // Assert
        var endpoints = await LicensingDataStore.GetAllEndpoints(true, default);
        var throughput = await LicensingDataStore.GetEndpointThroughputByQueueName([endpoint1.SanitizedName], default);

        var foundEndpoint = endpoints.Single();
        Assert.Multiple(() =>
        {
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpoint1.Id.Name));
            Assert.That(foundEndpoint.Id.ThroughputSource, Is.EqualTo(endpoint1.Id.ThroughputSource));
            Assert.That(throughput, Contains.Key(endpoint1.SanitizedName));
        });
        Assert.That(throughput[endpoint1.SanitizedName].Count, Is.EqualTo(1), "Should be only a single ThroughputData returned");
        Assert.That(throughput[endpoint1.SanitizedName].Single(), Has.Count.EqualTo(2), "Should be two days of throughput data");
    }

    [Test]
    public async Task Should_retrieve_matching_endpoint_when_same_source()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);
        await LicensingDataStore.SaveEndpoint(endpoint, default);

        // Act
        var foundEndpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit, default);

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null);
    }

    [Test]
    public async Task Should_not_retrieve_matching_endpoint_when_different_source()
    {
        // Arrange
        var endpoint = new Endpoint("Endpoint", ThroughputSource.Audit);
        await LicensingDataStore.SaveEndpoint(endpoint, default);

        // Act
        var foundEndpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Broker, default);

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
        await LicensingDataStore.SaveEndpoint(endpoint, default);

        // Act
        await LicensingDataStore.UpdateUserIndicatorOnEndpoints([new UpdateUserIndicator { Name = "Endpoint", UserIndicator = userIndicator }], default);

        // Assert
        var foundEndpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit, default);

        Assert.That(foundEndpoint, Is.Not.Null);
        Assert.That(foundEndpoint.UserIndicator, Is.EqualTo(userIndicator));
    }

    [Test]
    public async Task Should_not_add_endpoint_when_updating_user_indication()
    {
        // Arrange
        var userIndicatorUpate = new UpdateUserIndicator
        {
            Name = "Endpoint",
            UserIndicator = "someIndicator",
        };

        // Act
        await LicensingDataStore.UpdateUserIndicatorOnEndpoints([userIndicatorUpate], default);

        // Assert
        var allEndpoints = await LicensingDataStore.GetAllEndpoints(true, default);

        Assert.That(allEndpoints.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Should_update_indicators_on_all_endpoint_sources()
    {
        // Arrange
        var userIndicator = "someIndicator";

        var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit) { SanitizedName = "Endpoint" };
        var endpointMonitoring = new Endpoint("Endpoint", ThroughputSource.Monitoring) { SanitizedName = "Endpoint" };

        await LicensingDataStore.SaveEndpoint(endpointAudit, default);
        await LicensingDataStore.SaveEndpoint(endpointMonitoring, default);

        // Act
        await LicensingDataStore.UpdateUserIndicatorOnEndpoints([new UpdateUserIndicator { Name = "Endpoint", UserIndicator = userIndicator }], default);

        // Assert
        var foundEndpointAudit = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit, default);
        var foundEndpointMonitoring = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Monitoring, default);

        Assert.That(foundEndpointAudit, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(foundEndpointAudit.UserIndicator, Is.EqualTo(userIndicator));

            Assert.That(foundEndpointMonitoring, Is.Not.Null);
        });
        Assert.That(foundEndpointMonitoring.UserIndicator, Is.EqualTo(userIndicator));
    }

    [TestCase(10, 5, false)]
    [TestCase(10, 20, true)]
    public async Task Should_correctly_report_throughput_existence_for_X_days(int daysSinceLastThroughputEntry, int timeFrameToCheck, bool expectedValue)
    {
        // Arrange
        var endpointAudit = new Endpoint("Endpoint", ThroughputSource.Audit);
        await LicensingDataStore.SaveEndpoint(endpointAudit, default);

        await LicensingDataStore.RecordEndpointThroughput(
            endpointAudit.Id.Name,
            ThroughputSource.Audit,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysSinceLastThroughputEntry),
            50,
            default);

        Assert.That(await LicensingDataStore.IsThereThroughputForLastXDays(timeFrameToCheck, default), expectedValue ? Is.True : Is.False);
    }

    [TestCase(10, 5, ThroughputSource.Monitoring, ThroughputSource.Monitoring, false, false)]
    [TestCase(10, 20, ThroughputSource.Monitoring, ThroughputSource.Monitoring, false, true)]
    [TestCase(10, 20, ThroughputSource.Audit, ThroughputSource.Monitoring, false, false)]
    [TestCase(0, 5, ThroughputSource.Monitoring, ThroughputSource.Monitoring, false, false)]
    [TestCase(0, 5, ThroughputSource.Monitoring, ThroughputSource.Monitoring, true, true)]
    public async Task Should_correctly_report_throughput_existence_for_X_days_for_specific_source(int daysSinceLastThroughputEntry, int timeFrameToCheck, ThroughputSource throughputSourceToRecord, ThroughputSource throughputSourceToCheck, bool includeToday, bool expectedValue)
    {
        // Arrange
        var endpointAudit = new Endpoint("Endpoint", throughputSourceToRecord);
        await LicensingDataStore.SaveEndpoint(endpointAudit, default);

        await LicensingDataStore.RecordEndpointThroughput(
            endpointAudit.Id.Name,
            throughputSourceToRecord,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysSinceLastThroughputEntry),
            50,
            default);

        Assert.That(await LicensingDataStore.IsThereThroughputForLastXDaysForSource(timeFrameToCheck, throughputSourceToCheck, includeToday, default), expectedValue ? Is.True : Is.False);
    }
}