namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;
using ServiceControl.Transports.BrokerThroughput;
using System.Linq;

[TestFixture]
class ThroughputCollector_SanitizedNameGrouping_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {

        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_return_one_endpoint_in_grouping_in_throughput_summary_when_sanitizednames_are_same_but_different_case_when_using_cleansing()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("endpoint1", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "endpoint1")
                .WithThroughput(data: [50])
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Audit])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "Endpoint1")
                .WithThroughput(data: [60])
            .Build();

        var throughputCollector = new ThroughputCollector(DataStore, configuration.ThroughputSettings, configuration.AuditQuery, configuration.MonitoringService, [], new BrokerThroughputQuery_WithLowerCaseSanitizedNameCleanse());

        // Act
        var summary = await throughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(1));
        //should see 1 endpoint with both throughputs, and return 60 as the maximum one
        Assert.That(summary.Sum(s => s.MaxDailyThroughput), Is.EqualTo(60));
    }


    [Test]
    public async Task Should_return_one_endpoint_in_grouping_in_throughput_report_when_sanitizednames_are_same_but_different_case_when_using_cleansing()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("endpoint1", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "endpoint1")
                .WithThroughput(data: [50])
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Audit])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "Endpoint1")
                .WithThroughput(data: [60])
            .Build();

        var throughputCollector = new ThroughputCollector(DataStore, configuration.ThroughputSettings, configuration.AuditQuery, configuration.MonitoringService, [], new BrokerThroughputQuery_WithLowerCaseSanitizedNameCleanse());

        // Act
        var report = await throughputCollector.GenerateThroughputReport(null, null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1));
        //should see 1 endpoint with both throughputs, and return 60 as the maximum one
        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(60));
        Assert.That(report.ReportData.Queues.FirstOrDefault(f => f.QueueName == "Endpoint1").DailyThroughputFromAudit.Sum(s => s.MessageCount), Is.EqualTo(60));
        Assert.That(report.ReportData.Queues.FirstOrDefault(f => f.QueueName == "Endpoint1").DailyThroughputFromBroker.Sum(s => s.MessageCount), Is.EqualTo(50));
    }

    [Test]
    public async Task Should_return_two_endpoints_in_grouping_in_throughput_summary_when_sanitizednames_are_same_but_different_case_when_not_using_cleansing()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("endpoint1", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "endpoint1")
                .WithThroughput(data: [50])
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Audit])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "Endpoint1")
                .WithThroughput(data: [60])
            .Build();

        var throughputCollector = new ThroughputCollector(DataStore, configuration.ThroughputSettings, configuration.AuditQuery, configuration.MonitoringService, [], new BrokerThroughputQuery_WithNoSanitizedNameCleanse());

        // Act
        var summary = await throughputCollector.GetThroughputSummary(default);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary, Has.Count.EqualTo(2));
        //two different endpoints hence total throughput is a sum of both of them
        Assert.That(summary.Sum(s => s.MaxDailyThroughput), Is.EqualTo(110));
    }


    [Test]
    public async Task Should_return_two_endpoints_in_grouping_in_throughput_report_when_sanitizednames_are_same_but_different_case_when_not_using_cleansing()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint("endpoint1", sources: [ThroughputSource.Broker])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "endpoint1")
                .WithThroughput(data: [50])
            .AddEndpoint("Endpoint1", sources: [ThroughputSource.Audit])
            .ConfigureEndpoint(endpoint => endpoint.SanitizedName = "Endpoint1")
                .WithThroughput(data: [60])
            .Build();

        var throughputCollector = new ThroughputCollector(DataStore, configuration.ThroughputSettings, configuration.AuditQuery, configuration.MonitoringService, [], new BrokerThroughputQuery_WithNoSanitizedNameCleanse());

        // Act
        var report = await throughputCollector.GenerateThroughputReport(null, null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(2));
        //two different endpoints hence total throughput is a sum of both of them
        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(110));
    }

    class BrokerThroughputQuery_WithLowerCaseSanitizedNameCleanse : IBrokerThroughputQuery
    {
        public Dictionary<string, string> Data => throw new NotImplementedException();

        public string MessageTransport => "";

        public string ScopeType => "";

        public KeyDescriptionPair[] Settings => throw new NotImplementedException();

        public IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public bool HasInitialisationErrors(out string errorMessage) => throw new NotImplementedException();
        public void Initialize(ReadOnlyDictionary<string, string> settings) => throw new NotImplementedException();

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName) => endpointName;

        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName.ToLower();
    }

    class BrokerThroughputQuery_WithNoSanitizedNameCleanse : IBrokerThroughputQuery
    {
        public Dictionary<string, string> Data => throw new NotImplementedException();

        public string MessageTransport => "";

        public string ScopeType => "";

        public KeyDescriptionPair[] Settings => throw new NotImplementedException();

        public IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public bool HasInitialisationErrors(out string errorMessage) => throw new NotImplementedException();
        public void Initialize(ReadOnlyDictionary<string, string> settings) => throw new NotImplementedException();

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName) => endpointName;

        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
    }
}