namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuditThroughput;
using Contracts;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NuGet.Versioning;
using NUnit.Framework;
using ServiceControl.Transports.BrokerThroughput;

[TestFixture]
class AuditThroughputCollectorHostedService_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_handle_no_audit_remotes()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var auditQuery = new AuditQuery_NoAuditRemotes();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        //Assert
        Assert.That(auditQuery.InstanceParameter, Is.True);
    }

    [Test]
    public async Task Should_handle_exceptions_in_try_block_and_continue()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var auditQuery = new AuditQuery_ThrowingAnExceptionOnKnownEndpointsCall();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        //Assert
        Assert.That(auditQuery.InstanceParameter, Is.True);
    }

    [Test]
    public async Task Should_handle_cancellation_token_gracefully()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            configuration.AuditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await auditThroughputCollectorHostedService.StopAsync(token);

        //Assert
        Assert.That(auditThroughputCollectorHostedService.ExecuteTask?.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task Should_sanitize_endpoint_name()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        string endpointName = "e$ndpoint&1";
        var auditQuery = new AuditQuery_WithOneEndpoint(endpointName, 0, DateOnly.FromDateTime(DateTime.UtcNow));
        string endpointNameSanitized = "e-ndpoint-1";

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            auditQuery, fakeTimeProvider, new BrokerThroughputQuery_WithSanitization())
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        Endpoint foundEndpoint = await DataStore.GetEndpoint(endpointName, ThroughputSource.Audit);

        //Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected endpoint {endpointName} not found.");
        Assert.That(foundEndpoint.SanitizedName, Is.EqualTo(endpointNameSanitized),
            $"Endpoint {endpointName} name not sanitized correctly.");
    }

    [Test]
    public async Task Should_not_add_the_same_endpoint_throughput_if_runs_twice_on_the_same_day()
    {
        //Arrange
        var tokenSource1 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token1 = tokenSource1.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        string endpointName = "endpoint1";
        var throughputDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        long throughputCount = 5;
        var auditQuery = new AuditQuery_WithOneEndpoint(endpointName, throughputCount, throughputDate);

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            auditQuery: auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token1);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            } while (!token1.IsCancellationRequested);
        });
        await auditThroughputCollectorHostedService.StopAsync(token1);

        var tokenSource2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token2 = tokenSource2.Token;
        await auditThroughputCollectorHostedService.StartAsync(token2);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            } while (!token2.IsCancellationRequested);
        });
        await auditThroughputCollectorHostedService.StopAsync(token2);

        Endpoint foundEndpoint = await DataStore.GetEndpoint(endpointName, ThroughputSource.Audit);
        IDictionary<string, IEnumerable<ThroughputData>> foundEndpointThroughput =
            await DataStore.GetEndpointThroughputByQueueName([endpointName]);
        ThroughputData[] throughputData = foundEndpointThroughput[endpointName].ToArray();

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected to find endpoint {endpointName}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpointName), $"Expected name to be {endpointName}");
            Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        }
        Assert.That(foundEndpointThroughput.ContainsKey(endpointName), Is.True, $"Expected throughput for {endpointName}");

        Assert.That(throughputData.Length, Is.EqualTo(1), $"Expected 1 throughput data for {endpointName}");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(throughputData[0].ContainsKey(throughputDate), Is.True, $"Expected throughput for {throughputDate}");
            Assert.That(throughputData[0][throughputDate], Is.EqualTo(throughputCount), $"Expected throughput for {throughputDate} to be {throughputCount}");
        }
    }

    [Test]
    public async Task Should_only_create_new_endpoint_when_audit_counts_exist()
    {
        // Arrange
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var auditQuery = new AuditQuery_WithTwoEndpointsAndSelectiveCounts(
            endpointWithoutCounts: "EndpointNoData",
            endpointWithCounts: "EndpointWithData",
            throughputDate: date,
            throughputCount: 5);

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
            NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore,
            auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        // Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            } while (!token.IsCancellationRequested);
        });
        await auditThroughputCollectorHostedService.StopAsync(token);

        var endpointWithoutCounts = await DataStore.GetEndpoint("EndpointNoData", ThroughputSource.Audit, default);
        var endpointWithCounts = await DataStore.GetEndpoint("EndpointWithData", ThroughputSource.Audit, default);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpointWithoutCounts, Is.Null, "Endpoint with empty auditCounts should not be created");
            Assert.That(endpointWithCounts, Is.Not.Null, "Endpoint with auditCounts should be created");
        }
    }

    class AuditQuery_NoAuditRemotes : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName,
            CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<AuditCount>>([]);

        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken = default)
        {
            InstanceParameter = true;

            return Task.FromResult<IEnumerable<ServiceControlEndpoint>>([]);
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new ConnectionSettingsTestResult { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        public bool InstanceParameter { get; set; }
    }

    class AuditQuery_WithOneEndpoint : IAuditQuery
    {
        public AuditQuery_WithOneEndpoint(string endpointName, long throughputCount, DateOnly throughputDate)
        {
            EndpointName = endpointName;
            ThroughputCount = throughputCount;
            ThroughputDate = throughputDate;
        }

        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName,
            CancellationToken cancellationToken = default)
        {
            var auditCount = new AuditCount { UtcDate = ThroughputDate, Count = ThroughputCount };

            return Task.FromResult(new List<AuditCount> { auditCount }.AsEnumerable());
        }

        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken = default)
        {
            var scEndpoint = new ServiceControlEndpoint { Name = EndpointName, HeartbeatsEnabled = true };
            return Task.FromResult<IEnumerable<ServiceControlEndpoint>>([scEndpoint]);
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new ConnectionSettingsTestResult { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        string EndpointName { get; }
        long ThroughputCount { get; }
        DateOnly ThroughputDate { get; }
    }

    class AuditQuery_ThrowingAnExceptionOnKnownEndpointsCall : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken = default)
        {
            InstanceParameter = true;

            throw new Exception("Oops");
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public bool InstanceParameter { get; set; }
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

    class AuditQuery_WithTwoEndpointsAndSelectiveCounts : IAuditQuery
    {
        public AuditQuery_WithTwoEndpointsAndSelectiveCounts(
            string endpointWithoutCounts,
            string endpointWithCounts,
            DateOnly throughputDate,
            long throughputCount)
        {
            this.endpointWithoutCounts = endpointWithoutCounts;
            this.endpointWithCounts = endpointWithCounts;
            this.throughputDate = throughputDate;
            this.throughputCount = throughputCount;
        }

        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);
        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => _ => true;

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<ServiceControlEndpoint>>(
            [
                new ServiceControlEndpoint { Name = endpointWithoutCounts, HeartbeatsEnabled = true },
                new ServiceControlEndpoint { Name = endpointWithCounts, HeartbeatsEnabled = true }
            ]);

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken)
        {
            if (endpointUrlName == endpointWithCounts)
            {
                return Task.FromResult<IEnumerable<AuditCount>>([new AuditCount { UtcDate = throughputDate, Count = throughputCount }]);
            }

            return Task.FromResult<IEnumerable<AuditCount>>([]);
        }

        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) =>
            Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectionSettingsTestResult { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        readonly string endpointWithoutCounts;
        readonly string endpointWithCounts;
        readonly DateOnly throughputDate;
        readonly long throughputCount;
    }
}