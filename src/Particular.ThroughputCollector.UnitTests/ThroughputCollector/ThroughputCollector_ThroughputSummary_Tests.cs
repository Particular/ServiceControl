namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_ThroughputSummary_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }


    [Test]
    public async Task Should_remove_audit_error_and_servicecontrol_queue_from_summary()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });
        await DataStore.SaveEndpoint(ErrorEndpoint);
        await DataStore.RecordEndpointThroughput(ErrorEndpoint.Id, ErrorEndpoint.DailyThroughput);
        await DataStore.SaveEndpoint(AuditEndpoint);
        await DataStore.RecordEndpointThroughput(AuditEndpoint.Id, AuditEndpoint.DailyThroughput);
        await DataStore.SaveEndpoint(ServiceControlEndpoint);
        await DataStore.RecordEndpointThroughput(ServiceControlEndpoint.Id, ServiceControlEndpoint.DailyThroughput);

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");

    }
    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_summary_when_only_one_source_of_throughput()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
    }

    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_summary_when_multiple_sources_of_throughput()
    {
        EndpointsWithThroughputFromBrokerAndMonitoring.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
    }

    [Test]
    public async Task Should_return_correct_max_throughput_in_summary_when_data_only_from_one_source()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(3));

        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(55), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint3");

    }

    [Test]
    public async Task Should_return_correct_max_throughput_in_summary_when_multiple_sources()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(3));

        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
        Assert.That(summary.FirstOrDefault(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(57), $"Incorrect MaxDailyThroughput recorded for Endpoint3");
    }

    [Test]
    public async Task Should_return_correct_max_throughput_in_summary_when_endpoint_has_no_throughput()
    {
        EndpointsWithNoThroughput.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(1), "Invalid number of endpoints in throughput summary");
        Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(0), $"Incorrect MaxDailyThroughput recorded for {summary[0].Name}");
    }

    [Test]
    public async Task Should_return_correct_max_throughput_in_summary_when_data_from_multiple_sources_and_name_is_different()
    {
        EndpointsWithDifferentNamesButSameSanitizedNames.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var summary = await ThroughputCollector.GetThroughputSummary();

        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.Count, Is.EqualTo(1));

        //we want to see the name for the endpoint if one exists, not the broker sanitized name
        Assert.That(summary[0].Name, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

        //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
        Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
    }


    readonly List<Endpoint> EndpointsWithNoThroughput =
    [
        new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1" },
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerOnly =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoring =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoringAndAudit =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 61 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 64 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 57 }] },
        new Endpoint("Endpoint3", ThroughputSource.Monitoring) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] },
        new Endpoint("Endpoint3", ThroughputSource.Audit) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 42 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 47 }] }
    ];

    readonly List<Endpoint> EndpointsWithDifferentNamesButSameSanitizedNames =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1_", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];

    readonly Endpoint ServiceControlEndpoint = new("Particular.ServiceControl", ThroughputSource.Broker) { SanitizedName = "Particular.ServiceControl", EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()], DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
    readonly Endpoint AuditEndpoint = new("audit", ThroughputSource.Broker) { SanitizedName = "audit", EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()], DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
    readonly Endpoint ErrorEndpoint = new("error", ThroughputSource.Broker) { SanitizedName = "error", EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()], DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 500 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 600 }] };
}