namespace ServiceControl.Persistence.Tests.RavenDB.Throughput;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

// The usage report is built from the incremental time series that these calls write and read
[TestFixture]
class ThroughputTimeSeriesTests : RavenPersistenceTestBase
{
    static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Test]
    public async Task Throughput_recorded_for_the_same_day_adds_up()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-1), 30);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today, 12);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today, 30);

        var throughput = (await LicensingDataStore.GetEndpointThroughputByQueueName(["Endpoint"]))["Endpoint"].Single();

        Assert.That(throughput, Is.EqualTo(new Dictionary<DateOnly, long>
        {
            [Today.AddDays(-1)] = 30,
            [Today] = 42
        }));
    }

    [Test]
    public async Task Throughput_is_read_up_to_the_last_day_asked_for()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-2), 5);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-1), 6);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today, 7);

        var throughput = (await LicensingDataStore.GetEndpointThroughputByQueueName(["Endpoint"], Today))["Endpoint"].Single();

        Assert.That(throughput.Keys, Is.EqualTo(new[] { Today.AddDays(-2), Today.AddDays(-1) }));
    }

    [Test]
    public async Task The_last_collected_date_of_an_endpoint_is_its_newest_recorded_day()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-5), 10);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-1), 10);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-3), 10);

        var endpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

        Assert.That(endpoint.LastCollectedDate, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public async Task The_last_collected_date_is_read_for_every_requested_endpoint()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);
        await SaveEndpoint("Endpoint", ThroughputSource.Broker);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today, 10);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Broker, Today, 10);
        var audit = new EndpointIdentifier("Endpoint", ThroughputSource.Audit);
        var broker = new EndpointIdentifier("Endpoint", ThroughputSource.Broker);
        var unknown = new EndpointIdentifier("Unknown", ThroughputSource.Audit);

        var endpoints = (await LicensingDataStore.GetEndpoints([audit, broker, unknown])).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoints.Select(endpoint => endpoint.Id), Is.EqualTo(new[] { audit, broker, unknown }));
            Assert.That(endpoints[0].Endpoint.LastCollectedDate, Is.EqualTo(Today));
            Assert.That(endpoints[1].Endpoint.LastCollectedDate, Is.EqualTo(Today));
            Assert.That(endpoints[2].Endpoint, Is.Null);
        }
    }

    Task SaveEndpoint(string name, ThroughputSource source) =>
        LicensingDataStore.SaveEndpoint(new Endpoint(name, source) { SanitizedName = name });
}
