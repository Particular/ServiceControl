namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NuGet.Versioning;
using NUnit.Framework;
using Particular.ThroughputCollector.AuditThroughput;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Transports;

[TestFixture]
class AuditThroughputCollectorHostedService_Tests : ThroughputCollectorTestFixture
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
    public async Task Should_handle_no_audit_remotes()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var auditQuery = new AuditQuery_NoAuditRemotes();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider, null)
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
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider, null)
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
               NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, configuration.AuditQuery, fakeTimeProvider, null)
        { DelayStart = TimeSpan.Zero };

        //Act
        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await auditThroughputCollectorHostedService.StopAsync(token);

        //Assert
        Assert.IsTrue(auditThroughputCollectorHostedService.ExecuteTask?.IsCompletedSuccessfully);
    }

    [Test]
    public async Task Should_sanitize_endpoint_name()
    {
        //Arrange
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var endpointName = "e$ndpoint&1";
        var auditQuery = new AuditQuery_WithOneEndpointRequiringSanitization(endpointName);
        var endpointNameSanitized = "e-ndpoint-1";

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider, new BrokerThroughputQuery_WithSanitization())
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

        var foundEndpoint = await DataStore.GetEndpoint(endpointName, throughputSource: ThroughputSource.Audit);

        //Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected endpoint {endpointName} not found.");
        Assert.That(foundEndpoint.SanitizedName, Is.EqualTo(endpointNameSanitized), $"Endpoint {endpointName} name not sanitized correctly.");
    }

    class AuditQuery_NoAuditRemotes : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<AuditCount>>([]);
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);
        public IEnumerable<ServiceControlEndpoint> GetKnownEndpoints()
        {
            InstanceParameter = true;

            return [];
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) => Task.FromResult(new ConnectionSettingsTestResult() { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        public bool InstanceParameter { get; set; }
    }

    class AuditQuery_WithOneEndpointRequiringSanitization : IAuditQuery
    {
        public AuditQuery_WithOneEndpointRequiringSanitization(string endpointName)
        {
            EndpointName = endpointName;
        }
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<AuditCount>>([]);
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);
        public IEnumerable<ServiceControlEndpoint> GetKnownEndpoints()
        {
            var scEndpoint = new ServiceControlEndpoint
            {
                Name = EndpointName,
                HeartbeatsEnabled = true
            };
            return [scEndpoint];
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) => Task.FromResult(new ConnectionSettingsTestResult() { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        public string EndpointName { get; set; }
    }

    class AuditQuery_ThrowingAnExceptionOnKnownEndpointsCall : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);

        public IEnumerable<ServiceControlEndpoint> GetKnownEndpoints()
        {
            InstanceParameter = true;

            throw new Exception("Oops");
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) => throw new NotImplementedException();

        public bool InstanceParameter { get; set; }
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