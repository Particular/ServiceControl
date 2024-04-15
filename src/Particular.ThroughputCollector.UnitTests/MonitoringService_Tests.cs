namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.MonitoringThroughput;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Transports;

[TestFixture]
class MonitoringService_Tests : ThroughputCollectorTestFixture
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
    public async Task Should_record_new_endpoint_and_throughput()
    {
        // Arrange
        var message = new RecordEndpointThroughputData()
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = new EndpointThroughputData[] { new EndpointThroughputData { Name = "Endpoint1", Throughput = 15 } }
        };

        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await configuration.MonitoringService.RecordMonitoringThroughput(messageBytes, default);

        // Act
        var foundEndpoint = await DataStore.GetEndpoint("Endpoint1", ThroughputSource.Monitoring);
        var foundEndpointThroughput = await DataStore.GetEndpointThroughputByQueueName(["Endpoint1"]);
        var throughputData = foundEndpointThroughput["Endpoint1"].ToArray();

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, "Expected to find Endpoint1");
        Assert.That(foundEndpoint.Id.Name, Is.EqualTo("Endpoint1"), "Expected name to be Endpoint1");
        Assert.That(foundEndpoint.EndpointIndicators, Is.Not.Null, "Expected to find endpoint indicators");
        Assert.That(foundEndpoint.EndpointIndicators.Contains(EndpointIndicator.KnownEndpoint.ToString()), Is.True, "Expected KnownEndpoint indicator");

        Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        Assert.That(foundEndpointThroughput.ContainsKey("Endpoint1"), Is.True, "Expected throughput for Endpoint1");

        Assert.That(throughputData.Length, Is.EqualTo(1), "Expected 1 throughput data for Endpoint1");
        Assert.That(throughputData[0].ThroughputSource, Is.EqualTo(ThroughputSource.Monitoring), "Expected ThroughputSource to be Monitoring for Endpoint1");
        Assert.That(throughputData[0].Keys.Contains(DateOnly.FromDateTime(message.EndDateTime.Date)), Is.True, $"Expected throughput for {message.StartDateTime.Date} for Endpoint1");
        Assert.That(throughputData[0][DateOnly.FromDateTime(message.EndDateTime.Date)], Is.EqualTo(15), $"Expected throughput for {message.StartDateTime.Date} to be 15 for Endpoint1");
    }

    [Test]
    public async Task Should_sanitize_endpoint_name()
    {
        // Arrange
        var endpointName = "e$ndpoint*1";
        var message = new RecordEndpointThroughputData()
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = new EndpointThroughputData[] { new EndpointThroughputData { Name = endpointName, Throughput = 15 } }
        };

        var monitoringService = new MonitoringService(DataStore, new BrokerThroughputQuery_WithSanitization());
        var messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await monitoringService.RecordMonitoringThroughput(messageBytes, default);
        var endpointNameSanitized = "e-ndpoint-1";

        // Act
        var foundEndpoint = await DataStore.GetEndpoint(endpointName, ThroughputSource.Monitoring);

        // Assert        
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected endpoint {endpointName} not found.");
        Assert.That(foundEndpoint.SanitizedName, Is.EqualTo(endpointNameSanitized), $"Endpoint {endpointName} name not sanitized correctly.");
    }


    [Test]
    public async Task Should_return_successful_monitoring_connection_if_monitoring_throughput_exists()
    {
        // Arrange
        await DataStore.CreateBuilder()
           .AddEndpoint(sources: [ThroughputSource.Monitoring])
           .WithThroughput(days: 2)
           .Build();

        // Act
        var connectionSettingsResult = await configuration.MonitoringService.TestMonitoringConnection(default);

        // Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
        Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0), "Unexpected ConnectionErrorMessages");
    }

    class BrokerThroughputQuery_WithSanitization : IBrokerThroughputQuery
    {
        public Dictionary<string, string> Data => throw new NotImplementedException();

        public string MessageTransport => "";

        public string ScopeType => "";

        public KeyDescriptionPair[] Settings => throw new NotImplementedException();

        public IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken) => throw new NotImplementedException();
        public IAsyncEnumerable<ServiceControl.Transports.QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, CancellationToken cancellationToken) => throw new NotImplementedException();
        public bool HasInitialisationErrors(out string errorMessage) => throw new NotImplementedException();
        public void Initialise(FrozenDictionary<string, string> settings) => throw new NotImplementedException();
        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName)
        {
            var queueNameBuilder = new StringBuilder(endpointName);

            for (var i = 0; i < queueNameBuilder.Length; ++i)
            {
                var c = queueNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    queueNameBuilder[i] = '-';
                }
            }

            return queueNameBuilder.ToString();
        }
    }
}