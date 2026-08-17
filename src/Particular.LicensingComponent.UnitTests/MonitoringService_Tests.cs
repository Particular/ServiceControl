namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Concurrent;
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
using MonitoringThroughput;
using NUnit.Framework;
using Persistence;
using ServiceControl.Transports.BrokerThroughput;

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
        await configuration.MonitoringService.RecordMonitoringThroughput(messageBytes);

        // Act
        Endpoint foundEndpoint = await DataStore.GetEndpoint("Endpoint1", ThroughputSource.Monitoring);
        IDictionary<string, IEnumerable<ThroughputData>> foundEndpointThroughput =
            await DataStore.GetEndpointThroughputByQueueName(["Endpoint1"]);
        ThroughputData[] throughputData = foundEndpointThroughput["Endpoint1"].ToArray();

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, "Expected to find Endpoint1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo("Endpoint1"), "Expected name to be Endpoint1");
            Assert.That(foundEndpoint.EndpointIndicators, Is.Not.Null, "Expected to find endpoint indicators");
        }
        Assert.That(foundEndpoint.EndpointIndicators, Does.Contain(EndpointIndicator.KnownEndpoint.ToString()),
                    "Expected KnownEndpoint indicator");

        Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        Assert.That(foundEndpointThroughput.ContainsKey("Endpoint1"), Is.True, "Expected throughput for Endpoint1");

        Assert.That(throughputData.Length, Is.EqualTo(1), "Expected 1 throughput data for Endpoint1");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(throughputData[0].ThroughputSource, Is.EqualTo(ThroughputSource.Monitoring),
                    "Expected ThroughputSource to be Monitoring for Endpoint1");
            Assert.That(throughputData[0].Keys.Contains(DateOnly.FromDateTime(message.EndDateTime.Date)), Is.True,
                $"Expected throughput for {message.StartDateTime.Date} for Endpoint1");
            Assert.That(throughputData[0][DateOnly.FromDateTime(message.EndDateTime.Date)], Is.EqualTo(15),
                $"Expected throughput for {message.StartDateTime.Date} to be 15 for Endpoint1");
        }
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

        var monitoringService = new MonitoringService(DataStore, new BrokerThroughputQuery_WithSanitization());
        byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await monitoringService.RecordMonitoringThroughput(messageBytes);
        string endpointNameSanitized = "e-ndpoint-1";

        // Act
        Endpoint foundEndpoint = await DataStore.GetEndpoint(endpointName, ThroughputSource.Monitoring);

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
            await configuration.MonitoringService.TestMonitoringConnection();

        // Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0),
                "Unexpected ConnectionErrorMessages");
        }

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
            await configuration.MonitoringService.TestMonitoringConnection();

        // Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.False,
                    "Connection status should be unsuccessful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0),
                "Unexpected ConnectionErrorMessages");
        }

        Assert.That(connectionSettingsResult.Diagnostics, Is.Not.Null, "Expected diagnostic");
        Assert.That(
            connectionSettingsResult.Diagnostics.Contains("No throughput from Monitoring recorded",
                StringComparison.OrdinalIgnoreCase), Is.True, "Expected diagnostics not found");

        Approver.Verify(connectionSettingsResult.Diagnostics);
    }

    [Test]
    public async Task Should_record_throughput_for_every_endpoint_in_the_message()
    {
        // Arrange
        var dataStore = new YieldingLicensingDataStore();
        var monitoringService = new MonitoringService(dataStore);

        string[] endpointNames = [.. Enumerable.Range(1, 20).Select(i => $"Endpoint{i}")];
        var message = new RecordEndpointThroughputData
        {
            StartDateTime = DateTime.UtcNow.AddMinutes(-5),
            EndDateTime = DateTime.UtcNow,
            EndpointThroughputData = [.. endpointNames.Select(name => new EndpointThroughputData { Name = name, Throughput = 15 })]
        };

        // Act
        byte[] messageBytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await monitoringService.RecordMonitoringThroughput(messageBytes);

        // Assert
        string[] endpointsWithoutThroughput = [.. endpointNames.Where(name => !dataStore.RecordedThroughput.ContainsKey(name))];
        string[] unsavedEndpoints = [.. endpointNames.Where(name => !dataStore.SavedEndpoints.ContainsKey(name))];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unsavedEndpoints, Is.Empty,
                $"{unsavedEndpoints.Length} of {endpointNames.Length} endpoints were not saved when RecordMonitoringThroughput returned: {string.Join(", ", unsavedEndpoints)}");
            Assert.That(endpointsWithoutThroughput, Is.Empty,
                $"{endpointsWithoutThroughput.Length} of {endpointNames.Length} endpoints had no throughput recorded when RecordMonitoringThroughput returned: {string.Join(", ", endpointsWithoutThroughput)}");
        }

        Assert.That(dataStore.RecordedThroughput.Values, Is.All.EqualTo(15L), "Expected a throughput of 15 for every endpoint");
    }

    // Records only after suspending, so any write the service starts without awaiting is still
    // outstanding when RecordMonitoringThroughput returns.
    class YieldingLicensingDataStore : ILicensingDataStore
    {
        public ConcurrentDictionary<string, Endpoint> SavedEndpoints { get; } = new();

        public ConcurrentDictionary<string, long> RecordedThroughput { get; } = new();

        public async Task<Endpoint> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return SavedEndpoints.TryGetValue(id.Name, out Endpoint endpoint) ? endpoint : null;
        }

        public async Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            SavedEndpoints[endpoint.Id.Name] = endpoint;
        }

        public async Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource,
            IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            RecordedThroughput[endpointName] = throughput.Sum(t => t.MessageCount);
        }

        public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IEnumerable<(EndpointIdentifier Id, Endpoint Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<List<string>> GetReportMasks(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LicensedEndpointDetails> GetLicensedEndpointDetails(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveLicensedEndpointDetails(LicensedEndpointDetails result, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    class BrokerThroughputQuery_WithSanitization : IBrokerThroughputQuery
    {
        public Dictionary<string, string> Data => throw new NotImplementedException();

        public string MessageTransport => "";

        public string ScopeType => "";

        public KeyDescriptionPair[] Settings => throw new NotImplementedException();

        public IAsyncEnumerable<IBrokerQueue> GetQueueNames(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public bool HasInitialisationErrors(out string errorMessage) => throw new NotImplementedException();
        public void Initialize(ReadOnlyDictionary<string, string> settings) => throw new NotImplementedException();

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

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