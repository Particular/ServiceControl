namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Approvals;
using Contracts;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using MonitoringThroughput;
using NUnit.Framework;
using ServiceControl.Transports.BrokerThroughput;
using Shared;

[TestFixture]
class MonitoringService_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_record_new_endpoint_and_throughput()
    {
        // Arrange
        var message = new RecordEndpointThroughputData
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = new EndpointThroughputData[] { new() { Name = "Endpoint1", Throughput = 15 } }
        };

        byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await configuration.MonitoringService.RecordMonitoringThroughput(messageBytes, default);

        // Act
        Endpoint foundEndpoint = await DataStore.GetEndpoint("Endpoint1", ThroughputSource.Monitoring, default);
        IDictionary<string, IEnumerable<ThroughputData>> foundEndpointThroughput =
            await DataStore.GetEndpointThroughputByQueueName(["Endpoint1"], default);
        ThroughputData[] throughputData = foundEndpointThroughput["Endpoint1"].ToArray();

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, "Expected to find Endpoint1");
        Assert.Multiple(() =>
        {
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo("Endpoint1"), "Expected name to be Endpoint1");
            Assert.That(foundEndpoint.EndpointIndicators, Is.Not.Null, "Expected to find endpoint indicators");
        });
        Assert.That(foundEndpoint.EndpointIndicators, Does.Contain(EndpointIndicator.KnownEndpoint.ToString()),
                    "Expected KnownEndpoint indicator");

        Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        Assert.That(foundEndpointThroughput.ContainsKey("Endpoint1"), Is.True, "Expected throughput for Endpoint1");

        Assert.That(throughputData.Length, Is.EqualTo(1), "Expected 1 throughput data for Endpoint1");
        Assert.Multiple(() =>
        {
            Assert.That(throughputData[0].ThroughputSource, Is.EqualTo(ThroughputSource.Monitoring),
                    "Expected ThroughputSource to be Monitoring for Endpoint1");
            Assert.That(throughputData[0].Keys.Contains(DateOnly.FromDateTime(message.EndDateTime.Date)), Is.True,
                $"Expected throughput for {message.StartDateTime.Date} for Endpoint1");
            Assert.That(throughputData[0][DateOnly.FromDateTime(message.EndDateTime.Date)], Is.EqualTo(15),
                $"Expected throughput for {message.StartDateTime.Date} to be 15 for Endpoint1");
        });
    }

    [Test]
    public async Task Should_sanitize_endpoint_name()
    {
        // Arrange
        string endpointName = "e$ndpoint*1";
        var message = new RecordEndpointThroughputData
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = new EndpointThroughputData[] { new() { Name = endpointName, Throughput = 15 } }
        };

        var emptyConfig = new ConfigurationBuilder().Build();

        var monitoringService = new MonitoringService(
            DataStore,
            new ServiceControlSettings(emptyConfig),
            new BrokerThroughputQuery_WithSanitization()
        );
        byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await monitoringService.RecordMonitoringThroughput(messageBytes, default);
        string endpointNameSanitized = "e-ndpoint-1";

        // Act
        Endpoint foundEndpoint = await DataStore.GetEndpoint(endpointName, ThroughputSource.Monitoring, default);

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected endpoint {endpointName} not found.");
        Assert.That(foundEndpoint.SanitizedName, Is.EqualTo(endpointNameSanitized),
            $"Endpoint {endpointName} name not sanitized correctly.");
    }


    [Test]
    public async Task Should_return_successful_monitoring_connection_and_diagnostics_if_throughput_exists()
    {
        // Arrange
        DataStoreBuilder builder = DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Monitoring])
            .WithThroughput(days: 2);

        await builder.Build();

        // Act
        ConnectionSettingsTestResult connectionSettingsResult =
            await configuration.MonitoringService.TestMonitoringConnection(default);

        // Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.Multiple(() =>
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0),
                "Unexpected ConnectionErrorMessages");
        });

        Assert.That(connectionSettingsResult.Diagnostics, Is.Not.Null, "Expected diagnostic");
        Assert.That(
            connectionSettingsResult.Diagnostics.Contains("Throughput from Monitoring recorded",
                StringComparison.OrdinalIgnoreCase), Is.True, "Expected diagnostics not found");

        Approver.Verify(connectionSettingsResult.Diagnostics);
    }

    [Test]
    public async Task Should_return_error_monitoring_connection_and_diagnostics_if_no_throughput_in_last_30_days()
    {
        // Arrange
        DataStoreBuilder builder = DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Monitoring]);

        await builder.Build();

        // Act
        ConnectionSettingsTestResult connectionSettingsResult =
            await configuration.MonitoringService.TestMonitoringConnection(default);

        // Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.Multiple(() =>
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.False,
                    "Connection status should be unsuccessful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0),
                "Unexpected ConnectionErrorMessages");
        });

        Assert.That(connectionSettingsResult.Diagnostics, Is.Not.Null, "Expected diagnostic");
        Assert.That(
            connectionSettingsResult.Diagnostics.Contains("No throughput from Monitoring recorded",
                StringComparison.OrdinalIgnoreCase), Is.True, "Expected diagnostics not found");

        Approver.Verify(connectionSettingsResult.Diagnostics);
    }

    class BrokerThroughputQuery_WithSanitization : IBrokerThroughputQuery
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

        public string SanitizeEndpointName(string endpointName)
        {
            var queueNameBuilder = new StringBuilder(endpointName);

            for (int i = 0; i < queueNameBuilder.Length; ++i)
            {
                char c = queueNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    queueNameBuilder[i] = '-';
                }
            }

            return queueNameBuilder.ToString();
        }

        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
    }
}