namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Persistence.InMemory;
using ServiceControl.Transports;
using QueueThroughput = ServiceControl.Transports.QueueThroughput;

[TestFixture]
class BrokerThroughputCollectorHostedServiceTests
{
    [Test]
    public async Task EnsuringRepeatedEndpointsSanitizedNameContainsPostfix()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var dataStore = new InMemoryThroughputDataStore();
        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
                NullLogger<BrokerThroughputCollectorHostedService>.Instance,
                new MockedBrokerThroughputQuery(), new ThroughputSettings(Broker.None, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                dataStore, TimeProvider.System)
        { DelayStart = TimeSpan.Zero };
        await brokerThroughputCollectorHostedService.StartAsync(token);
        await (brokerThroughputCollectorHostedService.ExecuteTask! ?? Task.CompletedTask);

        Endpoint[] endpoints = (await dataStore.GetAllEndpoints(true, token)).ToArray();
        IEnumerable<string> sanitizedNames = endpoints.Select(endpoint => endpoint.SanitizedName);
        IEnumerable<string> queueNames = endpoints.Select(endpoint => endpoint.Id.Name);

        CollectionAssert.AreEquivalent(new[] { "sales", "sales1", "marketing", "customer" }, sanitizedNames);
        CollectionAssert.AreEquivalent(new[] { "sales@one", "sales@two", "marketing", "customer" }, queueNames);
    }

    [Test]
    public async Task EnsuringExceptionsAreHandledAndThroughputCollectorKeepsGoing()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var dataStore = new InMemoryThroughputDataStore();
        var fakeTimeProvider = new FakeTimeProvider();
        var mockedBrokerThroughputQueryThatThrowsExceptions = new MockedBrokerThroughputQueryThatThrowsExceptions();
        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
                NullLogger<BrokerThroughputCollectorHostedService>.Instance,
                mockedBrokerThroughputQueryThatThrowsExceptions, new ThroughputSettings(Broker.None, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                dataStore, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };
        await brokerThroughputCollectorHostedService.StartAsync(token);

        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        Assert.Greater(mockedBrokerThroughputQueryThatThrowsExceptions.GetQueueNamesCalls, 1);
        Assert.Greater(mockedBrokerThroughputQueryThatThrowsExceptions.GetGetThroughputPerDay, 1);
    }

    [Test]
    public async Task EnsuringHostedServiceStopCleanly()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var dataStore = new InMemoryThroughputDataStore();
        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
                NullLogger<BrokerThroughputCollectorHostedService>.Instance,
                new MockedBrokerThroughputQuery(), new ThroughputSettings(Broker.None, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
                dataStore, TimeProvider.System)
        { DelayStart = TimeSpan.Zero };
        await brokerThroughputCollectorHostedService.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await brokerThroughputCollectorHostedService.StopAsync(token);

        Assert.IsTrue(brokerThroughputCollectorHostedService.ExecuteTask?.IsCompletedSuccessfully);
    }

    class MockedBrokerThroughputQueryThatThrowsExceptions : IBrokerThroughputQuery
    {
        public void Initialise(FrozenDictionary<string, string> settings)
        { }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            GetGetThroughputPerDay++;

            await Task.CompletedTask;
            yield return new QueueThroughput();
            throw new Exception("bang");
        }

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (GetQueueNamesCalls++ % 2 == 0)
            {
                throw new Exception("bang");
            }

            await Task.CompletedTask;

            yield return new DefaultBrokerQueue("customer");
        }

        public int GetQueueNamesCalls { get; set; }
        public int GetGetThroughputPerDay { get; set; }
        public Dictionary<string, string> Data { get; }
        public string MessageTransport { get; }
        public string ScopeType { get; }
        public KeyDescriptionPair[] Settings { get; }
        public Task<(bool Success, List<string> Errors)> TestConnection(CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    class MockedBrokerThroughputQuery : IBrokerThroughputQuery
    {
        public void Initialise(FrozenDictionary<string, string> settings)
        { }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            yield break;
        }

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new DefaultBrokerQueue("sales@one") { SanitizedName = "sales" };
            yield return new DefaultBrokerQueue("sales@two") { SanitizedName = "sales" };
            yield return new DefaultBrokerQueue("marketing");
            yield return new DefaultBrokerQueue("customer");

            await Task.CompletedTask;
        }

        public Dictionary<string, string> Data { get; }
        public string MessageTransport { get; }
        public string ScopeType { get; }
        public KeyDescriptionPair[] Settings { get; }
        public Task<(bool Success, List<string> Errors)> TestConnection(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}