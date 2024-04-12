namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;

[TestFixture]
class ThroughputCollector_Report_Throughput_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {

        SetExtraDependencies = d =>
        {
            d.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            d.AddSingleton<IEndpointsApi, FakeEndpointApi>();
            d.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
        };

        return base.Setup();
    }


    [Test]
    public async Task Should_not_include_daily_throughput_for_non_nsb_endpoints()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpoint.ToString()).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpoint.ToString()).WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NotNServicebusEndpoint.ToString()).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3")?.DailyThroughputFromBroker?.Length, Is.EqualTo(0), $"Daily throughput should not be included for Endpoint3");
    }

    [Test]
    public async Task Should_include_daily_throughput_for_endpoints_with_userIndicator_other_than_non_nsb_endpoints()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpoint.ToString()).WithThroughput(days: 2)
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpointNoLongerInUse.ToString()).WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpointSendOnly.ToString()).WithThroughput(days: 2)
            .AddEndpoint("Endpoint4", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpointScaledOut.ToString()).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(4), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint1");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint2");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint3");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint4")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint4");
    }

    [TestCase(ThroughputSource.Audit)]
    [TestCase(ThroughputSource.Broker)]
    [TestCase(ThroughputSource.Monitoring)]
    public async Task Should_return_correct_number_of_endpoints_in_report_when_only_one_source_of_throughput(ThroughputSource source)
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .AddEndpoint(sources: [source]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
    }

    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_report_when_multiple_sources_of_throughput()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
    }

    [TestCase(ThroughputSource.Audit)]
    [TestCase(ThroughputSource.Broker)]
    [TestCase(ThroughputSource.Monitoring)]
    public async Task Should_return_correct_throughput_in_report_when_data_only_from_one_source(ThroughputSource source)
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [source]).WithThroughput(data: [50, 55])
            .AddEndpoint("Endpoint2", sources: [source]).WithThroughput(data: [60, 65])
            .AddEndpoint("Endpoint3", sources: [source]).WithThroughput(data: [75, 50])
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(55), $"Incorrect Throughput recorded for Endpoint1");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint3");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(195), $"Incorrect TotalThroughput recorded");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorded");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_multiple_sources()
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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint1");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(57), $"Incorrect Throughput recorded for Endpoint3");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(187), $"Incorrect TotalThroughput recorded");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorde");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_endpoint_has_no_throughput()
    {
        // Arrange
        await DataStore.CreateBuilder().AddEndpoint().Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1), "Invalid number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(0), $"Incorrect Throughput recorded for {report.ReportData.Queues[0].QueueName}");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(0), $"Incorrect TotalThroughput recorded");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_data_from_multiple_sources_and_name_is_different()
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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1));

        //we want to see the name for the endpoint if one exists, not the broker sanitized name
        Assert.That(report.ReportData.Queues[0].QueueName, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

        //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
        Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint1");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(75), $"Incorrect TotalThroughput recorded");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded");
    }
}