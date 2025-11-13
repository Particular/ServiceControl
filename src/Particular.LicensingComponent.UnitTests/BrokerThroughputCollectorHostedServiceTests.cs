namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrokerThroughput;
using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Persistence.InMemory;
using ServiceControl.Transports.BrokerThroughput;
using Shared;

[TestFixture]
class BrokerThroughputCollectorHostedServiceTests
{
    [Test]
    public async Task EnsuringRepeatedEndpointsSanitizedNameContainsPostfix()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var configuration = new ConfigurationBuilder().Build();
        var dataStore = new InMemoryLicensingDataStore();

        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
            NullLogger<BrokerThroughputCollectorHostedService>.Instance,
            new MockedBrokerThroughputQuery(),
            new ThroughputSettings(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            dataStore,
            TimeProvider.System,
            new PlatformEndpointHelper(new ServiceControlSettings(configuration)),
            configuration
        )
        {
            DelayStart = TimeSpan.Zero
        };
        await brokerThroughputCollectorHostedService.StartAsync(token);
        await (brokerThroughputCollectorHostedService.ExecuteTask! ?? Task.CompletedTask);

        Endpoint[] endpoints = (await dataStore.GetAllEndpoints(true, token)).ToArray();
        IEnumerable<string> sanitizedNames = endpoints.Select(endpoint => endpoint.SanitizedName);
        IEnumerable<string> queueNames = endpoints.Select(endpoint => endpoint.Id.Name);

        Assert.That(sanitizedNames, Is.EquivalentTo(new[]
        {
            "sales", "sales1", "marketing", "customer"
        }));
        Assert.That(queueNames, Is.EquivalentTo(new[]
        {
            "sales@one", "sales@two", "marketing", "customer"
        }));
    }

    [Test]
    public async Task EnsuringStartDatePassedToGetThroughputPerDayIsAlwaysOneDayAheadFromLastCollectionData()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var lastCollectionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var dataStore = new InMemoryLicensingDataStore();
        await dataStore.SaveEndpoint(new Endpoint("marketing", ThroughputSource.Broker), token);
        await dataStore.RecordEndpointThroughput("marketing", ThroughputSource.Broker,
            new List<EndpointDailyThroughput>([new EndpointDailyThroughput(lastCollectionDate, 10)]), token);
        await dataStore.SaveEndpoint(new Endpoint("customer", ThroughputSource.Broker), token);
        await dataStore.RecordEndpointThroughput("customer", ThroughputSource.Broker,
            new List<EndpointDailyThroughput>([new EndpointDailyThroughput(lastCollectionDate, 100)]), token);
        var mockedBrokerThroughputQueryThatRecordsDate = new MockedBrokerThroughputQueryThatRecordsDate();
        var emptyConfig = new ConfigurationBuilder().Build();

        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
            NullLogger<BrokerThroughputCollectorHostedService>.Instance,
            mockedBrokerThroughputQueryThatRecordsDate,
            new ThroughputSettings(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            dataStore,
            TimeProvider.System,
            new PlatformEndpointHelper(new ServiceControlSettings(emptyConfig)),
            emptyConfig
            )
        {
            DelayStart = TimeSpan.Zero
        };
        await brokerThroughputCollectorHostedService.StartAsync(token);
        await (brokerThroughputCollectorHostedService.ExecuteTask! ?? Task.CompletedTask);

        foreach (var startDate in mockedBrokerThroughputQueryThatRecordsDate.StartDates)
        {
            Assert.That(startDate, Is.EqualTo(lastCollectionDate.AddDays(1)));
        }
    }

    [Test]
    public async Task EnsuringExceptionsAreHandledAndThroughputCollectorKeepsGoing()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var dataStore = new InMemoryLicensingDataStore();
        var fakeTimeProvider = new FakeTimeProvider();
        var mockedBrokerThroughputQueryThatThrowsExceptions = new MockedBrokerThroughputQueryThatThrowsExceptions();
        var emptyConfig = new ConfigurationBuilder().Build();

        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
            NullLogger<BrokerThroughputCollectorHostedService>.Instance,
            mockedBrokerThroughputQueryThatThrowsExceptions,
            new ThroughputSettings(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            dataStore,
            fakeTimeProvider,
            new PlatformEndpointHelper(new ServiceControlSettings(emptyConfig)),
            emptyConfig
            )
        {
            DelayStart = TimeSpan.Zero
        };
        await brokerThroughputCollectorHostedService.StartAsync(token);

        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        Assert.Multiple(() =>
        {
            Assert.That(mockedBrokerThroughputQueryThatThrowsExceptions.GetQueueNamesCalls, Is.GreaterThan(1));
            Assert.That(mockedBrokerThroughputQueryThatThrowsExceptions.GetGetThroughputPerDay, Is.GreaterThan(1));
        });
    }

    [Test]
    public async Task EnsuringHostedServiceStopCleanly()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;

        var dataStore = new InMemoryLicensingDataStore();
        var emptyConfig = new ConfigurationBuilder().Build();

        using var brokerThroughputCollectorHostedService = new BrokerThroughputCollectorHostedService(
            NullLogger<BrokerThroughputCollectorHostedService>.Instance,
            new MockedBrokerThroughputQuery(),
            new ThroughputSettings(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty),
            dataStore,
            TimeProvider.System,
            new PlatformEndpointHelper(new ServiceControlSettings(emptyConfig)),
            emptyConfig
            )
        {
            DelayStart = TimeSpan.Zero
        };
        await brokerThroughputCollectorHostedService.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await brokerThroughputCollectorHostedService.StopAsync(token);

        Assert.That(brokerThroughputCollectorHostedService.ExecuteTask?.IsCompletedSuccessfully, Is.True);
    }

    class MockedBrokerThroughputQueryThatThrowsExceptions : IBrokerThroughputQuery
    {
        public bool HasInitialisationErrors(out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }

        public void Initialize(ReadOnlyDictionary<string, string> settings)
        {
        }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            GetGetThroughputPerDay++;

            await Task.CompletedTask;
            yield return new QueueThroughput();
            throw new Exception("bang");
        }

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
            [EnumeratorCancellation] CancellationToken cancellationToken)
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
        public KeyDescriptionPair[] Settings { get; } = [];

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName) => endpointName;
        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
    }

    class MockedBrokerThroughputQuery : IBrokerThroughputQuery
    {
        public bool HasInitialisationErrors(out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }

        public void Initialize(ReadOnlyDictionary<string, string> settings)
        {
        }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            yield break;
        }

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new DefaultBrokerQueue("sales@one")
            {
                SanitizedName = "sales"
            };
            yield return new DefaultBrokerQueue("sales@two")
            {
                SanitizedName = "sales"
            };
            yield return new DefaultBrokerQueue("marketing");
            yield return new DefaultBrokerQueue("customer");

            await Task.CompletedTask;
        }

        public Dictionary<string, string> Data { get; }
        public string MessageTransport { get; }
        public string ScopeType { get; }
        public KeyDescriptionPair[] Settings { get; } = [];

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName) => endpointName;
        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
    }

    class MockedBrokerThroughputQueryThatRecordsDate : IBrokerThroughputQuery
    {
        public List<DateOnly> StartDates = [];

        public bool HasInitialisationErrors(out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }

        public void Initialize(ReadOnlyDictionary<string, string> settings)
        {
        }

        public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            StartDates.Add(startDate);

            yield break;
        }

        public async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new DefaultBrokerQueue("marketing");
            yield return new DefaultBrokerQueue("customer");

            await Task.CompletedTask;
        }

        public Dictionary<string, string> Data { get; }
        public string MessageTransport { get; }
        public string ScopeType { get; }
        public KeyDescriptionPair[] Settings { get; } = [];

        public Task<(bool Success, List<string> Errors, string Diagnostics)> TestConnection(
            CancellationToken cancellationToken) => throw new NotImplementedException();

        public string SanitizeEndpointName(string endpointName) => endpointName;
        public string SanitizedEndpointNameCleanser(string endpointName) => endpointName;
    }
}