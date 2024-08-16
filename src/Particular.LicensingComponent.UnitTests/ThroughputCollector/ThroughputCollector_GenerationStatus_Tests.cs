namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_GenerationStatus_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test, UseNonBrokerTransport]
    public async Task Should_return_ReportCanBeGenerated_true_when_not_using_broker_and_no_broker_throughput_for_last_30_days()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Audit]).WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), days: 2)
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.True);
        Assert.That(reportGenerationState.Reason, Is.EqualTo(""));
    }


    [Test]
    public async Task Should_return_ReportCanBeGenerated_true_when_using_broker_and_there_is_broker_throughput_for_last_30_days()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint().WithThroughput(source: ThroughputSource.Broker, startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), days: 2)
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.True);
        Assert.That(reportGenerationState.Reason, Is.EqualTo(""));
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_using_broker_and_no_broker_throughput_for_last_30_days()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint()
            .WithThroughput(source: ThroughputSource.Broker, startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-32), days: 2)
            .WithThroughput(source: ThroughputSource.Audit, startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), days: 2)
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
        Assert.That(reportGenerationState.Reason, Does.Contain("one day"), "Report generation failure reason does not contain 'one day'");
        Assert.That(reportGenerationState.Reason, Does.Contain("broker"), "Report generation failure reason does not contain 'broker'");
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_throughput_exists_only_for_today()
    {
        // Arrange
        await DataStore.CreateBuilder().AddEndpoint()
            .WithThroughput(source: ThroughputSource.Broker, startDate: DateOnly.FromDateTime(DateTime.UtcNow))
            .WithThroughput(source: ThroughputSource.Audit, startDate: DateOnly.FromDateTime(DateTime.UtcNow))
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
        Assert.That(reportGenerationState.Reason, Does.Contain("one day"), "Report generation failure reason does not contain 'one day'");
        Assert.That(reportGenerationState.Reason, Does.Contain("broker"), "Report generation failure reason does not contain 'broker'");
    }
}