﻿namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NuGet.Versioning;
using NUnit.Framework;
using Particular.LicensingComponent.AuditThroughput;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;
using ServiceControl.Transports;

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
        var auditQuery = new AuditQuery_WithOneEndpoint(endpointName, 0, DateOnly.FromDateTime(DateTime.UtcNow));
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

        var foundEndpoint = await DataStore.GetEndpoint(endpointName, throughputSource: ThroughputSource.Audit, default);

        //Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected endpoint {endpointName} not found.");
        Assert.That(foundEndpoint.SanitizedName, Is.EqualTo(endpointNameSanitized), $"Endpoint {endpointName} name not sanitized correctly.");
    }

    [Test]
    public async Task Should_not_add_the_same_endpoint_throughput_if_runs_twice_on_the_same_day()
    {
        //Arrange
        var tokenSource1 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        CancellationToken token1 = tokenSource1.Token;
        var fakeTimeProvider = new FakeTimeProvider();
        var endpointName = "endpoint1";
        var throughputDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        long throughputCount = 5;
        var auditQuery = new AuditQuery_WithOneEndpoint(endpointName, throughputCount, throughputDate);

        using var auditThroughputCollectorHostedService = new AuditThroughputCollectorHostedService(
                NullLogger<AuditThroughputCollectorHostedService>.Instance, configuration.ThroughputSettings, DataStore, auditQuery, fakeTimeProvider, null)
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

        var foundEndpoint = await DataStore.GetEndpoint(endpointName, throughputSource: ThroughputSource.Audit, default);
        var foundEndpointThroughput = await DataStore.GetEndpointThroughputByQueueName([endpointName], default);
        var throughputData = foundEndpointThroughput[endpointName].ToArray();

        // Assert
        Assert.That(foundEndpoint, Is.Not.Null, $"Expected to find endpoint {endpointName}");
        Assert.That(foundEndpoint.Id.Name, Is.EqualTo(endpointName), $"Expected name to be {endpointName}");

        Assert.That(foundEndpointThroughput, Is.Not.Null, "Expected endpoint throughput");
        Assert.That(foundEndpointThroughput.ContainsKey(endpointName), Is.True, $"Expected throughput for {endpointName}");

        Assert.That(throughputData.Length, Is.EqualTo(1), $"Expected 1 throughput data for {endpointName}");
        Assert.That(throughputData[0].ContainsKey(throughputDate), Is.True, $"Expected throughput for {throughputDate}");
        Assert.That(throughputData[0][throughputDate], Is.EqualTo(throughputCount), $"Expected throughput for {throughputDate} to be {throughputCount}");
    }

    class AuditQuery_NoAuditRemotes : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<AuditCount>>([]);
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);
        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken)
        {
            InstanceParameter = true;

            return Task.FromResult<IEnumerable<ServiceControlEndpoint>>([]);
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) => Task.FromResult(new ConnectionSettingsTestResult() { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

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

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken)
        {
            var auditCount = new AuditCount
            {
                UtcDate = ThroughputDate,
                Count = ThroughputCount
            };

            return Task.FromResult(new List<AuditCount> { auditCount }.AsEnumerable());
        }
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);
        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken)
        {
            var scEndpoint = new ServiceControlEndpoint
            {
                Name = EndpointName,
                HeartbeatsEnabled = true
            };
            return Task.FromResult<IEnumerable<ServiceControlEndpoint>>([scEndpoint]);
        }

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken) => Task.FromResult(new ConnectionSettingsTestResult() { ConnectionSuccessful = true, ConnectionErrorMessages = [] });

        string EndpointName { get; set; }
        long ThroughputCount { get; set; }
        DateOnly ThroughputDate { get; set; }
    }

    class AuditQuery_ThrowingAnExceptionOnKnownEndpointsCall : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r => true;

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken) => Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken)
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