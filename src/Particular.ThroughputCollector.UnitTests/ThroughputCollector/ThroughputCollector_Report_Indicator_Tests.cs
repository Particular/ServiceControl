﻿namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Indicator_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_indicate_known_endpoint_if_at_least_one_instance_of_it_exists_in_the_sources()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint("Endpoint1_", sources: [ThroughputSource.Audit]).WithThroughput(days: 2)
            .ConfigureEndpoint(endpoint =>
            {
                endpoint.SanitizedName = "Endpoint1";
                endpoint.EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()];
            })
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Length, Is.EqualTo(1));

        Assert.That(report.ReportData.Queues[0].EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()), Is.True, $"Incorrect IsKnownEndpoint recorded for {report.ReportData.Queues[0].QueueName}");
    }

    [Test]
    public async Task Should_return_correct_user_indicators_when_multiple_throughput_sources()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
            .ConfigureEndpoint(ThroughputSource.Broker, endpoint => endpoint.UserIndicator = UserIndicator.NServicebusEndpointScaledOut.ToString())
            .WithThroughput(ThroughputSource.Broker, days: 2)
            .WithThroughput(ThroughputSource.Monitoring, days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Length, Is.EqualTo(1));

        Assert.That(report.ReportData.Queues[0].UserIndicator, Is.EqualTo(UserIndicator.NServicebusEndpointScaledOut.ToString()));
    }
}