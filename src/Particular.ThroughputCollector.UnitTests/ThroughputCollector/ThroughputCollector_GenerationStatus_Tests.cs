namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;

[TestFixture]
class ThroughputCollector_GenerationStatus_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d =>
        {
            d.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            d.AddSingleton<IEndpointsApi, FakeEndpointApi>();
            d.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
            d.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
        };

        return base.Setup();
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_no_throughput_for_last_30_days()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-32), days: 2)
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-32), days: 2)
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_true_when_there_is_throughput_for_last_30_days()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), days: 2)
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), days: 2)
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.True);
    }

    [Test]
    public async Task Should_return_ReportCanBeGenerated_false_when_throughput_exists_only_for_today()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow))
            .AddEndpoint().WithThroughput(startDate: DateOnly.FromDateTime(DateTime.UtcNow))
            .Build();

        // Act
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    }
}