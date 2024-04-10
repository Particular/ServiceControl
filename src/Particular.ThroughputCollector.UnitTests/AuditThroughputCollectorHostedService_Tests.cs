namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
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

[TestFixture]
class AuditThroughputCollectorHostedService_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.None;

    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

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
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var auditQuery = new AuditQuery_NoAuditRemotes();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };
        await auditThroughputCollectorHostedService.StartAsync(token);

        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        Assert.That(auditQuery.InstanceParameter, Is.True);
    }

    [Test]
    public async Task Should_handle_exceptions_in_try_block_and_continue()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var auditQuery = new AuditQuery_ThrowingAnExceptionOnKnownEndpointsCall();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };
        await auditThroughputCollectorHostedService.StartAsync(token);

        await Task.Run(async () =>
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                fakeTimeProvider.Advance(TimeSpan.FromDays(1));
            } while (!token.IsCancellationRequested);
        });

        Assert.That(auditQuery.InstanceParameter, Is.True);
    }

    [Test]
    public async Task Should_handle_cancellation_token_gracefully()
    {
        var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token = tokenSource.Token;
        var fakeTimeProvider = new FakeTimeProvider();

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
               NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, configuration.AuditQuery, fakeTimeProvider)
        { DelayStart = TimeSpan.Zero };

        await auditThroughputCollectorHostedService.StartAsync(token);
        await Task.Delay(TimeSpan.FromSeconds(2), token);
        await auditThroughputCollectorHostedService.StopAsync(token);

        Assert.IsTrue(auditThroughputCollectorHostedService.ExecuteTask?.IsCompletedSuccessfully);
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
}