namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

class LicensingDataStoreEFTests : PersistenceTestBase
{
    static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Test]
    public async Task Recording_the_same_day_twice_adds_to_that_day()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Monitoring);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Monitoring, Today, 30);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Monitoring, Today, 12);

        var throughput = await GetThroughput("Endpoint");

        Assert.That(throughput[Today], Is.EqualTo(42));
    }

    // Monitoring reports the same day from several instances, so the additions have to survive
    // landing at the same moment rather than one overwriting the other.
    [Test]
    public async Task Concurrent_recordings_of_the_same_day_all_count()
    {
        const int writers = 10;
        await SaveEndpoint("Endpoint", ThroughputSource.Monitoring);

        var recordings = Enumerable.Range(0, writers)
            .Select(_ => LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Monitoring, Today, 5));

        await Task.WhenAll(recordings);

        var throughput = await GetThroughput("Endpoint");

        Assert.That(throughput[Today], Is.EqualTo(writers * 5));
    }

    [Test]
    public async Task Endpoints_are_matched_regardless_of_name_casing()
    {
        await SaveEndpoint("SalesEndpoint", ThroughputSource.Broker);

        await LicensingDataStore.RecordEndpointThroughput("salesendpoint", ThroughputSource.Broker, Today, 7);

        var endpoint = await LicensingDataStore.GetEndpoint("SALESENDPOINT", ThroughputSource.Broker);

        Assert.That(endpoint, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.Id.Name, Is.EqualTo("SalesEndpoint"), "the reported casing is what is stored");
            Assert.That(endpoint.LastCollectedDate, Is.EqualTo(Today));
        }
    }

    [Test]
    public async Task Last_collected_date_is_the_newest_recorded_day()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-5), 10);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-1), 10);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Audit, Today.AddDays(-3), 10);

        var endpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

        Assert.That(endpoint, Is.Not.Null);
        Assert.That(endpoint.LastCollectedDate, Is.EqualTo(Today.AddDays(-1)));
    }

    [Test]
    public async Task Last_collected_date_is_unset_when_nothing_was_recorded()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);

        var endpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Audit);

        Assert.That(endpoint, Is.Not.Null);
        Assert.That(endpoint.LastCollectedDate, Is.EqualTo(default(DateOnly)));
    }

    [Test]
    public async Task Endpoints_are_returned_for_every_requested_id_including_the_unknown_ones()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Audit);
        await SaveEndpoint("Endpoint", ThroughputSource.Broker);

        var requested = new[]
        {
            new EndpointIdentifier("Endpoint", ThroughputSource.Audit),
            new EndpointIdentifier("Endpoint", ThroughputSource.Broker),
            new EndpointIdentifier("Endpoint", ThroughputSource.Monitoring),
            new EndpointIdentifier("Unknown", ThroughputSource.Audit)
        };

        var results = (await LicensingDataStore.GetEndpoints(requested)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.Count.EqualTo(requested.Length));
            Assert.That(results[0].Endpoint, Is.Not.Null);
            Assert.That(results[1].Endpoint, Is.Not.Null);
            Assert.That(results[2].Endpoint, Is.Null, "same name, but no endpoint for that source");
            Assert.That(results[3].Endpoint, Is.Null);
        }
    }

    [Test]
    public async Task Platform_endpoints_are_excluded_when_asked_for()
    {
        await LicensingDataStore.SaveEndpoint(
            new Endpoint("Platform", ThroughputSource.Broker)
            {
                SanitizedName = "Platform",
                EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()]
            });
        await SaveEndpoint("Regular", ThroughputSource.Broker);

        var withPlatform = await LicensingDataStore.GetAllEndpoints(true);
        var withoutPlatform = await LicensingDataStore.GetAllEndpoints(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withPlatform.Count(), Is.EqualTo(2));
            Assert.That(withoutPlatform.Single().Id.Name, Is.EqualTo("Regular"));
        }
    }

    [Test]
    public async Task Endpoint_indicators_survive_a_round_trip()
    {
        string[] indicators = [EndpointIndicator.KnownEndpoint.ToString(), EndpointIndicator.DelayBinding.ToString()];

        await LicensingDataStore.SaveEndpoint(
            new Endpoint("Endpoint", ThroughputSource.Broker)
            {
                SanitizedName = "Endpoint",
                EndpointIndicators = indicators,
                Scope = "vhost",
                UserIndicator = UserIndicator.NServiceBusEndpoint.ToString()
            });

        var endpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Broker);

        Assert.That(endpoint, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.EndpointIndicators, Is.EquivalentTo(indicators));
            Assert.That(endpoint.Scope, Is.EqualTo("vhost"));
            Assert.That(endpoint.UserIndicator, Is.EqualTo(UserIndicator.NServiceBusEndpoint.ToString()));
        }
    }

    // A report covers the last 14 months, and older rows are left in place rather than filtered out
    // by the retention sweep, so the read has to do the filtering.
    [Test]
    public async Task Throughput_older_than_the_reported_window_is_not_returned()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Broker);

        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Broker, Today.AddMonths(-15), 99);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Broker, Today.AddDays(-1), 5);

        var throughput = await GetThroughput("Endpoint");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(throughput, Has.Count.EqualTo(1));
            Assert.That(throughput[Today.AddDays(-1)], Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Recording_throughput_for_an_unknown_endpoint_throws()
    {
        Assert.That(async () => await LicensingDataStore.RecordEndpointThroughput("Unknown", ThroughputSource.Broker, Today, 1),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Saving_an_endpoint_again_replaces_it_and_keeps_its_throughput()
    {
        await SaveEndpoint("Endpoint", ThroughputSource.Broker);
        await LicensingDataStore.RecordEndpointThroughput("Endpoint", ThroughputSource.Broker, Today, 11);

        await LicensingDataStore.SaveEndpoint(
            new Endpoint("Endpoint", ThroughputSource.Broker) { SanitizedName = "Endpoint", Scope = "updated" });

        var endpoint = await LicensingDataStore.GetEndpoint("Endpoint", ThroughputSource.Broker);
        var throughput = await GetThroughput("Endpoint");

        Assert.That(endpoint, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.Scope, Is.EqualTo("updated"));
            Assert.That(throughput[Today], Is.EqualTo(11));
        }
    }

    Task SaveEndpoint(string name, ThroughputSource source) =>
        LicensingDataStore.SaveEndpoint(new Endpoint(name, source) { SanitizedName = name });

    async Task<ThroughputData> GetThroughput(string queueName)
    {
        var throughput = await LicensingDataStore.GetEndpointThroughputByQueueName([queueName]);

        return throughput[queueName].Single();
    }
}
