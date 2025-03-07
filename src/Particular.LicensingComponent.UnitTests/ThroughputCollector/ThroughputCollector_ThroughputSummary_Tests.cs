namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_ThroughputSummary_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_remove_audit_error_and_servicecontrol_queue_from_summary()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint().WithThroughput(days: 2)
            .AddEndpoint().WithThroughput(days: 2)
            .AddEndpoint().WithThroughput(days: 2)
            .AddEndpoint("Particular.ServiceControl")
            .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()]).WithThroughput(days: 2)
            .AddEndpoint("audit")
            .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()]).WithThroughput(days: 2)
            .AddEndpoint("error")
            .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()]).WithThroughput(days: 2)
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
    }

    [TestCase(ThroughputSource.Audit)]
    [TestCase(ThroughputSource.Broker)]
    [TestCase(ThroughputSource.Monitoring)]
    public async Task Should_return_correct_number_of_endpoints_in_summary_when_only_one_source_of_throughput(ThroughputSource source)
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
    }

    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_summary_when_multiple_sources_of_throughput()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring]).WithThroughput(days: 2)
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(3), $"Incorrect number of endpoints in throughput summary");
    }

    [TestCase(ThroughputSource.Audit)]
    [TestCase(ThroughputSource.Broker)]
    [TestCase(ThroughputSource.Monitoring)]
    public async Task Should_return_correct_max_daily_throughput_in_summary_when_data_only_from_one_source(ThroughputSource source)
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [source]).WithThroughput(data: [50, 55])
            .AddEndpoint("Endpoint2", sources: [source]).WithThroughput(data: [60, 65])
            .AddEndpoint("Endpoint3", sources: [source]).WithThroughput(data: [75, 50])
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(3), $"Incorrect number of endpoints in throughput summary");

        Assert.Multiple(() =>
        {
            Assert.That(summary.Single(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(55), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
            Assert.That(summary.Single(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
            Assert.That(summary.Single(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint3");
        });
    }

    [Test]
    public async Task Should_return_correct_max_daily_throughput_in_summary_when_multiple_sources()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, data: [50, 55])
                .WithThroughput(ThroughputSource.Monitoring, data: [60, 65])
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, data: [60, 65])
                .WithThroughput(ThroughputSource.Audit, data: [61, 64])
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, data: [50, 57])
                .WithThroughput(ThroughputSource.Monitoring, data: [40, 45])
                .WithThroughput(ThroughputSource.Audit, data: [42, 47])
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(summary.First(w => w.Name == "Endpoint1").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
            Assert.That(summary.First(w => w.Name == "Endpoint2").MaxDailyThroughput, Is.EqualTo(65), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
            Assert.That(summary.First(w => w.Name == "Endpoint3").MaxDailyThroughput, Is.EqualTo(57), $"Incorrect MaxDailyThroughput recorded for Endpoint3");
        });
    }

    [Test]
    public async Task Should_return_correct_max_monthly_throughput_in_summary_when_multiple_sources()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker])
            .WithThroughput(new ThroughputData([
                new EndpointDailyThroughput(new DateOnly(2025, 1, 10), 50),
                new EndpointDailyThroughput(new DateOnly(2025, 1, 15), 50),
                new EndpointDailyThroughput(new DateOnly(2025, 1, 16), 150),
                new EndpointDailyThroughput(new DateOnly(2025, 2, 20), 160),
                new EndpointDailyThroughput(new DateOnly(2025, 3, 25), 65),
                new EndpointDailyThroughput(new DateOnly(2025, 4, 30), 70),
                new EndpointDailyThroughput(new DateOnly(2025, 5, 1), 75)]))
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker])
            .WithThroughput(new ThroughputData([
                new EndpointDailyThroughput(new DateOnly(2025, 1, 10), 60),
                new EndpointDailyThroughput(new DateOnly(2025, 1, 15), 65),
                new EndpointDailyThroughput(new DateOnly(2025, 5, 20), 165),
                new EndpointDailyThroughput(new DateOnly(2025, 3, 25), 65),
                new EndpointDailyThroughput(new DateOnly(2025, 9, 30), 70)]))
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(CancellationToken.None);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(2));

        Assert.Multiple(() =>
        {
            Assert.That(summary.First(w => w.Name == "Endpoint1").MaxMonthlyThroughput, Is.EqualTo(250), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
            Assert.That(summary.First(w => w.Name == "Endpoint2").MaxMonthlyThroughput, Is.EqualTo(165), $"Incorrect MaxDailyThroughput recorded for Endpoint2");
        });
    }

    [Test]
    public async Task Should_return_correct_max_daily_throughput_in_summary_when_endpoint_has_no_throughput()
    {
        // Arrange
        await DataStore.CreateBuilder().AddEndpoint().Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(1), "Invalid number of endpoints in throughput summary");
        Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(0), $"Incorrect MaxDailyThroughput recorded for {summary[0].Name}");
    }

    [Test]
    public async Task Should_return_correct_max_daily_throughput_in_summary_when_data_from_multiple_sources_and_name_is_different()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker])
                .WithThroughput(data: [50, 75])
            .AddEndpoint("Endpoint1_", sources: [ThroughputSource.Audit])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "Endpoint1")
                .WithThroughput(data: [60, 65])
            .Build();

        // Act
        var summary = await ThroughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(1));

        Assert.Multiple(() =>
        {
            //we want to see the name for the endpoint if one exists, not the broker sanitized name
            Assert.That(summary[0].Name, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

            //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
            Assert.That(summary[0].MaxDailyThroughput, Is.EqualTo(75), $"Incorrect MaxDailyThroughput recorded for Endpoint1");
        });
    }
}