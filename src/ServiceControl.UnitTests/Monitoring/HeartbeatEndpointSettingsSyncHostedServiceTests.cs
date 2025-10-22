namespace ServiceControl.UnitTests.Monitoring;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Persistence;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Monitoring;
using ServiceControl.Operations;

[TestFixture]
public class HeartbeatEndpointSettingsSyncHostedServiceTests
{
    [Test]
    public async Task Should_handle_cancellation_token_gracefully()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            new MockMonitoringDataStore([]),
            new MockEndpointSettingsStore([]),
            new MockEndpointInstanceMonitoring([]), new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = true
                }
            },
            fakeTimeProvider,
            NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance
        )
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(service.ExecuteTask?.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Should_delete_settings_from_endpoints_that_are_no_longer_live()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var mockEndpointSettingsStore = new MockEndpointSettingsStore([
            new EndpointSettings
            {
                Name = "Sales", TrackInstances = true
            },
            new EndpointSettings
            {
                Name = "Orders", TrackInstances = false
            }
        ]);
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            new MockMonitoringDataStore([]),
            mockEndpointSettingsStore,
            new MockEndpointInstanceMonitoring([]), new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = true
                }
            },
            fakeTimeProvider, NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance)
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(mockEndpointSettingsStore.Deleted.Count, Is.EqualTo(2));
        Assert.That(mockEndpointSettingsStore.Deleted, Contains.Item("Sales").And.Contain("Orders"));
    }

    [Test]
    public async Task Should_set_the_default_for_settings_if_does_not_exist_already()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        const bool expectedTrackInstancesInitialValue = true;
        var fakeTimeProvider = new FakeTimeProvider();
        var mockEndpointSettingsStore = new MockEndpointSettingsStore([]);
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            new MockMonitoringDataStore(
                []),
            mockEndpointSettingsStore,
            new MockEndpointInstanceMonitoring([]),
            new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = expectedTrackInstancesInitialValue
                }
            },
            fakeTimeProvider, NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance)
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(mockEndpointSettingsStore.Updated.Count, Is.EqualTo(1));
        Assert.That(mockEndpointSettingsStore.Updated[0].TrackInstances,
            Is.EqualTo(expectedTrackInstancesInitialValue));
        Assert.That(mockEndpointSettingsStore.Updated[0].Name,
            Is.Empty);
    }

    [Test]
    public async Task Should_not_set_the_default_if_already_exists()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        const bool expectedTrackInstancesInitialValue = false;
        var fakeTimeProvider = new FakeTimeProvider();
        var mockEndpointSettingsStore = new MockEndpointSettingsStore([
            new EndpointSettings
            {
                Name = string.Empty, TrackInstances = expectedTrackInstancesInitialValue
            }
        ]);
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            new MockMonitoringDataStore(
                []),
            mockEndpointSettingsStore,
            new MockEndpointInstanceMonitoring([]),
            new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = expectedTrackInstancesInitialValue
                }
            },
            fakeTimeProvider, NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance)
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(mockEndpointSettingsStore.Updated.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task
        Should_delete_endpoint_monitoring_instance_data_if_instance_is_not_heartbeating_and_tracking_instances_is_disabled()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        const string endpointName1 = "Sales";
        Guid instanceA = DeterministicGuid.MakeId(endpointName1, "A");
        Guid instanceB = DeterministicGuid.MakeId(endpointName1, "B");
        Guid instanceC = DeterministicGuid.MakeId(endpointName1, "C");
        var mockMonitoringDataStore = new MockMonitoringDataStore(
        [
            new KnownEndpoint
            {
                EndpointDetails = new EndpointDetails
                {
                    Name = endpointName1
                }
            }
        ]);
        var mockEndpointInstanceMonitoring = new MockEndpointInstanceMonitoring([
            new EndpointsView
            {
                IsSendingHeartbeats = false, Name = endpointName1, Id = instanceA
            },
            new EndpointsView
            {
                IsSendingHeartbeats = false, Name = endpointName1, Id = instanceB
            },
            new EndpointsView
            {
                IsSendingHeartbeats = false, Name = endpointName1, Id = instanceC
            },
            new EndpointsView
            {
                IsSendingHeartbeats = true, Name = endpointName1, Id = DeterministicGuid.MakeId(endpointName1, "D")
            }
        ]);
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            mockMonitoringDataStore,
            new MockEndpointSettingsStore([
                new EndpointSettings
                {
                    Name = endpointName1, TrackInstances = false
                }
            ]),
            mockEndpointInstanceMonitoring, new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = true
                }
            },
            fakeTimeProvider, NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance)
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(mockMonitoringDataStore.Deleted.Count, Is.EqualTo(2));
        Assert.That(mockMonitoringDataStore.Deleted, Is.EquivalentTo(new List<Guid>([instanceA, instanceB])));
        Assert.That(mockEndpointInstanceMonitoring.Removed.Count, Is.EqualTo(2));
        Assert.That(mockEndpointInstanceMonitoring.Removed, Is.EquivalentTo(new List<Guid>([instanceA, instanceB])));
    }

    [Test]
    public async Task
        Should_not_delete_endpoint_monitoring_instance_data_if_instance_is_not_heartbeating_and_tracking_instances_is_enabled()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        const string endpointName1 = "Sales";
        Guid instanceA = DeterministicGuid.MakeId(endpointName1, "A");
        var mockMonitoringDataStore = new MockMonitoringDataStore(
        [
            new KnownEndpoint
            {
                EndpointDetails = new EndpointDetails
                {
                    Name = endpointName1
                }
            }
        ]);
        var mockEndpointInstanceMonitoring = new MockEndpointInstanceMonitoring([
            new EndpointsView
            {
                IsSendingHeartbeats = false, Name = endpointName1, Id = instanceA
            },
            new EndpointsView
            {
                IsSendingHeartbeats = true, Name = endpointName1, Id = DeterministicGuid.MakeId(endpointName1, "B")
            }
        ]);
        var service = new HeartbeatEndpointSettingsSyncHostedService(
            mockMonitoringDataStore,
            new MockEndpointSettingsStore([
                new EndpointSettings
                {
                    Name = endpointName1, TrackInstances = true
                }
            ]),
            mockEndpointInstanceMonitoring, new Settings
            {
                ServiceControl =
                {
                    TrackInstancesInitialValue = true
                }
            },
            fakeTimeProvider, NullLogger<HeartbeatEndpointSettingsSyncHostedService>.Instance)
        {
            DelayStart = TimeSpan.Zero
        };

        await service.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await service.StopAsync(token);

        Assert.That(mockMonitoringDataStore.Deleted.Count, Is.EqualTo(0));
        Assert.That(mockEndpointInstanceMonitoring.Removed.Count, Is.EqualTo(0));
    }

    class MockEndpointInstanceMonitoring(EndpointsView[] endpointsViews) : IEndpointInstanceMonitoring
    {
        public Task CheckEndpoints(DateTime threshold) => throw new NotImplementedException();

        public Task DetectEndpointFromHeartbeatStartup(EndpointDetails newEndpointDetails, DateTime startedAt) =>
            throw new NotImplementedException();

        public void DetectEndpointFromPersistentStore(EndpointDetails endpointDetails, bool monitored) =>
            throw new NotImplementedException();

        public Task DisableMonitoring(Guid id) => throw new NotImplementedException();

        public Task EnableMonitoring(Guid id) => throw new NotImplementedException();

        public Task EndpointDetected(EndpointDetails newEndpointDetails) => throw new NotImplementedException();

        public EndpointsView[] GetEndpoints() => endpointsViews;

        public List<KnownEndpointsView> GetKnownEndpoints() => throw new NotImplementedException();

        public EndpointMonitoringStats GetStats() => throw new NotImplementedException();

        public bool HasEndpoint(Guid endpointId) => throw new NotImplementedException();

        public bool IsMonitored(Guid id) => throw new NotImplementedException();

        public bool IsNewInstance(EndpointDetails newEndpointDetails) => throw new NotImplementedException();

        public void RecordHeartbeat(EndpointInstanceId endpointInstanceId, DateTime timestamp) =>
            throw new NotImplementedException();

        public void RemoveEndpoint(Guid endpointId) => Removed.Add(endpointId);

        public List<Guid> Removed { get; } = [];
    }

    class MockEndpointSettingsStore(EndpointSettings[] settings) : IEndpointSettingsStore
    {
        public IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings() => settings.ToAsyncEnumerable();

        public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken token)
        {
            Updated.Add(settings);
            return Task.CompletedTask;
        }

        public Task Delete(string name, CancellationToken cancellationToken)
        {
            Deleted.Add(name);
            return Task.CompletedTask;
        }

        public readonly List<EndpointSettings> Updated = [];

        public readonly HashSet<string> Deleted = [];
    }

    class MockMonitoringDataStore(KnownEndpoint[] knownEndpoints) : IMonitoringDataStore
    {
        public Task CreateIfNotExists(EndpointDetails endpoint) => throw new NotImplementedException();

        public Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
            throw new NotImplementedException();

        public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored) =>
            throw new NotImplementedException();

        public Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
            throw new NotImplementedException();

        public Task Delete(Guid endpointId)
        {
            Deleted.Add(endpointId);
            return Task.CompletedTask;
        }

        public List<Guid> Deleted { get; } = [];

        public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints() =>
            Task.FromResult<IReadOnlyList<KnownEndpoint>>(knownEndpoints);
    }
}