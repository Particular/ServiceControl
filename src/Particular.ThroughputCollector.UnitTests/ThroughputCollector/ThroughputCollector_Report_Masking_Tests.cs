﻿namespace Particular.ThroughputCollector.UnitTests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Masking_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {

        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_mask_endpoint_names_when_mask_provided()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport(["Endpoint1"], "", default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Null, $"QueueName not masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "REDACTED1"), Is.Not.Null, $"QueueName not masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
    }

    [Test]
    public async Task Should_not_mask_endpoint_names_when_no_mask_provided()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint2", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint3", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "", default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Invalid TotalQueues on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName.Contains("REDACTED")), Is.Null, $"QueueNames should not be masked on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1"), Is.Not.Null, $"QueueName Endpoint1 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3"), Is.Not.Null, $"QueueName Endpoint2 not found on report");
    }
}