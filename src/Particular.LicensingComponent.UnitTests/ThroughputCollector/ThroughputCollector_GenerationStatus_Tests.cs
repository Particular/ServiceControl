﻿namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_GenerationStatus_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

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
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
        Assert.That(reportGenerationState.Reason.Contains("24"), Is.True);
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
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.True);
        Assert.That(reportGenerationState.Reason, Is.EqualTo(""));
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
        var reportGenerationState = await ThroughputCollector.GetReportGenerationState(default);

        // Assert
        Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    }
}