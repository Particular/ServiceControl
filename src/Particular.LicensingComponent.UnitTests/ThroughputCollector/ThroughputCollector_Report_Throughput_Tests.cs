namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts;
using Infrastructure;
using NUnit.Framework;
using Particular.Approvals;
using Particular.LicensingComponent.Report;

[TestFixture]
class ThroughputCollector_Report_Throughput_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {

        SetExtraDependencies = d => { };
        return base.Setup();
    }


    [Test]
    public async Task Should_not_include_daily_throughput_for_non_nsb_endpoints()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServiceBusEndpoint.ToString())
            .WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServiceBusEndpoint.ToString())
            .WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NotNServiceBusEndpoint.ToString())
            .WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

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
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NServiceBusEndpoint.ToString())
            .WithThroughput(days: 2)
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint =>
                endpoint.UserIndicator = UserIndicator.NServiceBusEndpointNoLongerInUse.ToString())
            .WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(
                endpoint => endpoint.UserIndicator = UserIndicator.SendOnlyEndpoint.ToString())
            .WithThroughput(days: 2)
            .AddEndpoint("Endpoint4", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.PlannedToDecommission.ToString())
            .WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(4), $"Incorrect number of endpoints in throughput report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint1");
            Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint2");
            Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint3");
            Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint4")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Daily throughput should be included for Endpoint4");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(55), $"Incorrect Throughput recorded for Endpoint1");
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint3");
        });

        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(195), $"Incorrect TotalThroughput recorded");
            Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorded");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint1");
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
            Assert.That(report.ReportData.Queues.First(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(57), $"Incorrect Throughput recorded for Endpoint3");
        });

        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(187), $"Incorrect TotalThroughput recorded");
            Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorded");
        });
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_endpoint_has_no_throughput()
    {
        // Arrange
        await DataStore.CreateBuilder().AddEndpoint().Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1), "Invalid number of endpoints in throughput report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(0), $"Incorrect Throughput recorded for {report.ReportData.Queues[0].QueueName}");

            Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(0), $"Incorrect TotalThroughput recorded");
            Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1));

        Assert.Multiple(() =>
        {
            //we want to see the name for the endpoint if one exists, not the broker sanitized name
            Assert.That(report.ReportData.Queues[0].QueueName, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

            //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
            Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint1");

            Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(75), $"Incorrect TotalThroughput recorded");
            Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded");
        });
    }

    [Test]
    public async Task Should_generate_correct_report()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, data: [50, 55], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .WithThroughput(ThroughputSource.Monitoring, data: [60, 65], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()])
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, data: [60, 65], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .WithThroughput(ThroughputSource.Audit, data: [61, 64], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()])
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, data: [50, 57], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .WithThroughput(ThroughputSource.Monitoring, data: [40, 45], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .WithThroughput(ThroughputSource.Audit, data: [42, 47], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
                .ConfigureEndpoint(endpoint => endpoint.EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()])
            .AddEndpoint("Endpoint4", sources: [ThroughputSource.Broker])
                .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.PlannedToDecommission.ToString())
                .WithThroughput(ThroughputSource.Broker, data: [42, 47], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
            .AddEndpoint("Endpoint5", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.UserIndicator = UserIndicator.NotNServiceBusEndpoint.ToString())
            .WithThroughput(ThroughputSource.Broker, data: [15, 4], startDate: DateOnly.FromDateTime(new DateTime(2024, 4, 24)))
            .Build();

        var expectedReportMasks = new List<string> { "Endpoint1" };
        await DataStore.SaveReportMasks(expectedReportMasks, default);

        var expectedBrokerVersion = "1.2";
        var expectedScopeType = "testingScope";
        await DataStore.SaveBrokerMetadata(new BrokerMetadata(expectedScopeType, new Dictionary<string, string> { [EnvironmentDataType.BrokerVersion.ToString()] = expectedBrokerVersion }), default);

        var expectedAuditVersionSummary = new Dictionary<string, int> { ["4.3.6"] = 2 };
        var expectedAuditTransportSummary = new Dictionary<string, int> { ["AzureServiceBus"] = 2 };
        await DataStore.SaveAuditServiceMetadata(new AuditServiceMetadata(expectedAuditVersionSummary, expectedAuditTransportSummary), default);

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("2.3.1", new DateTime(2024, 4, 25), default);
        var reportString = System.Text.Json.JsonSerializer.Serialize(report, SerializationOptions.IndentedWithNoEscaping);

        // Assert
        Approver.Verify(reportString,
            scrubber: input => input.Replace(report.Signature, "SIGNATURE"));
    }
}