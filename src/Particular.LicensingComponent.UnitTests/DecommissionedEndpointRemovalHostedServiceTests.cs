namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Persistence;
using Persistence.InMemory;
using Report;

[TestFixture]
class DecommissionedEndpointRemovalHostedServiceTests
{
    [TestCase(-25, true)]
    [TestCase(-24, true)]
    [TestCase(-23, false)]
    [TestCase(0, false)]
    [TestCase(24, false)]
    public async Task EnsuringCreatedDateAllowsOneDayBeforeRemovingEndpoints(int hoursSinceNow, bool shouldBeRemoved)
    {
        var timeProvider = new FakeTimeProvider();
        var endpoint = new Endpoint("sales", ThroughputSource.Broker)
        {
            CreatedDate = timeProvider.GetUtcNow().UtcDateTime.AddHours(hoursSinceNow)
        };
        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(endpoint);
        var throughputCollector = new MockedThroughputCollector(_ => [CreateThroughput([endpoint])]);

        await RunService(dataStore, throughputCollector, timeProvider);

        var remainingEndpoints = (await dataStore.GetAllEndpoints(true)).ToArray();
        Assert.That(remainingEndpoints, shouldBeRemoved ? Is.Empty : Is.EquivalentTo(new[] { endpoint }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EnsuringEndpointsWithoutCreatedDateOrThroughputAreRemoved(bool hasEmptyThroughputData)
    {
        var endpoint = new Endpoint("sales", ThroughputSource.Broker);
        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(endpoint);
        var throughput = hasEmptyThroughputData
            ? CreateThroughput([endpoint], [], [])
            : CreateThroughput([endpoint]);
        var throughputCollector = new MockedThroughputCollector(_ => [throughput]);

        await RunService(dataStore, throughputCollector, new FakeTimeProvider());

        Assert.That(await dataStore.GetAllEndpoints(true), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(10)]
    public async Task EnsuringEndpointsWithThroughputAreNotRemoved(long messageCount)
    {
        var timeProvider = new FakeTimeProvider();
        var endpoint = new Endpoint("sales", ThroughputSource.Broker);
        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(endpoint);
        var throughputData = new ThroughputData
        {
            [DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)] = messageCount
        };
        var throughputCollector = new MockedThroughputCollector(_ => [CreateThroughput([endpoint], [], throughputData)]);

        await RunService(dataStore, throughputCollector, timeProvider);

        Assert.That(await dataStore.GetAllEndpoints(true), Is.EquivalentTo(new[] { endpoint }));
    }

    [Test]
    public async Task EnsuringOnlyOlderEndpointsInTheSameGroupAreRemoved()
    {
        var timeProvider = new FakeTimeProvider();
        var olderEndpoint = new Endpoint("sales", ThroughputSource.Broker)
        {
            CreatedDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-2)
        };
        var legacyEndpoint = new Endpoint("sales", ThroughputSource.Audit);
        var newEndpoint = new Endpoint("sales", ThroughputSource.Monitoring)
        {
            CreatedDate = timeProvider.GetUtcNow().UtcDateTime
        };
        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(olderEndpoint);
        await dataStore.SaveEndpoint(legacyEndpoint);
        await dataStore.SaveEndpoint(newEndpoint);
        var throughputCollector = new MockedThroughputCollector(_ => [CreateThroughput([olderEndpoint, legacyEndpoint, newEndpoint])]);

        await RunService(dataStore, throughputCollector, timeProvider);

        Assert.That(await dataStore.GetAllEndpoints(true), Is.EquivalentTo(new[] { newEndpoint }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EnsuringCleanupRunsAgainTheNextDayAfterCollection(bool firstCollectionThrows)
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var token = tokenSource.Token;
        var timeProvider = new FakeTimeProvider();
        var endpoint = new Endpoint("sales", ThroughputSource.Broker)
        {
            CreatedDate = timeProvider.GetUtcNow().UtcDateTime
        };
        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(endpoint);
        var throughputCollector = new MockedThroughputCollector(call =>
        {
            if (firstCollectionThrows && call == 1)
            {
                throw new InvalidOperationException("Collection failed");
            }

            return [CreateThroughput([endpoint])];
        });
        using var service = new DecommissionedEndpointRemovalHostedService(
            NullLogger<DecommissionedEndpointRemovalHostedService>.Instance,
            dataStore, throughputCollector, timeProvider);

        await service.StartAsync(token);
        await throughputCollector.FirstCollectionCompleted.Task.WaitAsync(token);
        Assert.That(await dataStore.GetAllEndpoints(true, token), Is.EquivalentTo(new[] { endpoint }));

        timeProvider.Advance(TimeSpan.FromHours(23));
        Assert.That(throughputCollector.GetDistinctEndpointThroughputCalls, Is.EqualTo(1));

        timeProvider.Advance(TimeSpan.FromHours(1));
        await throughputCollector.SecondCollectionCompleted.Task.WaitAsync(token);
        await service.StopAsync(token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(throughputCollector.GetDistinctEndpointThroughputCalls, Is.EqualTo(2));
            Assert.That(await dataStore.GetAllEndpoints(true, token), Is.Empty);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EnsuringRemovalExceptionsAreHandledAndCleanupKeepsGoing(bool throwsCancellationException)
    {
        var failedEndpoint = new Endpoint("sales", ThroughputSource.Broker);
        var otherEndpoint = new Endpoint("marketing", ThroughputSource.Broker);
        var dataStore = new MockedLicensingDataStoreThatThrowsExceptions(failedEndpoint.Id, throwsCancellationException);
        await dataStore.SaveEndpoint(failedEndpoint);
        await dataStore.SaveEndpoint(otherEndpoint);
        var throughputCollector = new MockedThroughputCollector(_ =>
            [CreateThroughput([failedEndpoint]), CreateThroughput([otherEndpoint])]);

        await RunService(dataStore, throughputCollector, new FakeTimeProvider());

        Assert.That(await dataStore.GetAllEndpoints(true), Is.EquivalentTo([failedEndpoint]));
    }

    [Test]
    public async Task EnsuringHostedServiceStopsCleanly()
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var token = tokenSource.Token;
        var throughputCollector = new MockedThroughputCollector(_ => []);
        using var service = new DecommissionedEndpointRemovalHostedService(
            NullLogger<DecommissionedEndpointRemovalHostedService>.Instance,
            new InMemoryLicensingDataStore(), throughputCollector, new FakeTimeProvider());

        await service.StartAsync(token);
        await throughputCollector.FirstCollectionCompleted.Task.WaitAsync(token);
        await service.StopAsync(token);

        Assert.That(service.ExecuteTask?.IsCompletedSuccessfully, Is.True);
    }

    static EndpointThroughput CreateThroughput(Endpoint[] endpoints, params ThroughputData[] throughputData) =>
        new(endpoints[0].Id.Name, endpoints.GroupBy(endpoint => endpoint.Id.Name).Single(), [.. throughputData]);

    static async Task RunService(ILicensingDataStore dataStore, MockedThroughputCollector throughputCollector, FakeTimeProvider timeProvider)
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var token = tokenSource.Token;
        using var service = new DecommissionedEndpointRemovalHostedService(
            NullLogger<DecommissionedEndpointRemovalHostedService>.Instance,
            dataStore, throughputCollector, timeProvider);

        await service.StartAsync(token);
        await throughputCollector.FirstCollectionCompleted.Task.WaitAsync(token);
        await service.StopAsync(token);
    }

    class MockedLicensingDataStoreThatThrowsExceptions(EndpointIdentifier failedEndpointId, bool throwsCancellationException)
        : InMemoryLicensingDataStore, ILicensingDataStore
    {
        public new Task RemoveEndpoints(EndpointIdentifier[] endpointIds, CancellationToken cancellationToken = default)
        {
            if (endpointIds.Contains(failedEndpointId))
            {
                throw throwsCancellationException ? new OperationCanceledException() : new InvalidOperationException("Removal failed");
            }

            return base.RemoveEndpoints(endpointIds, cancellationToken);
        }
    }

    class MockedThroughputCollector(Func<int, IEnumerable<EndpointThroughput>> getThroughput) : IThroughputCollector
    {
        public int GetDistinctEndpointThroughputCalls { get; private set; }
        public TaskCompletionSource FirstCollectionCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondCollectionCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<EndpointThroughput> GetDistinctEndpointThroughput(DateOnly? throughputMaxDate = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            var call = ++GetDistinctEndpointThroughputCalls;
            try
            {
                foreach (var throughput in getThroughput(call))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return throughput;
                }
            }
            finally
            {
                if (call == 1)
                {
                    FirstCollectionCompleted.TrySetResult();
                }
                else
                {
                    SecondCollectionCompleted.TrySetResult();
                }
            }
        }

        public Task<List<EndpointThroughputSummary>> GetThroughputSummary(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateUserIndicatorsOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ThroughputConnectionSettings> GetThroughputConnectionSettingsInformation(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ConnectionTestResults> TestConnectionSettings(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<SignedReport> GenerateThroughputReport(string spVersion, DateTime? reportEndDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReportGenerationState> GetReportGenerationState(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<string>> GetReportMasks(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateReportMasks(List<string> reportMaskUpdates, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
