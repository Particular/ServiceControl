namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

[TestFixture]
class AuditQuery_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.None;

    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        SetExtraDependencies = d =>
        {
            d.AddSingleton<IConfigurationApi, ConfigurationApi_ReturningOneAuditConfigs>();
            d.AddSingleton<IEndpointsApi, EndpointsApi_ReturningTwoEndpoints>();
            d.AddSingleton<IAuditCountApi, AuditCountApi_ReturningThreeAuditCounts>();
        };

        return base.Setup();
    }

    [Test]
    public void Should_return_known_endpoints_if_any()
    {
        var endpoints = configuration.AuditQuery.GetKnownEndpoints();

        Assert.That(endpoints, Is.Not.Null, "Endpoints should be found");
        Assert.That(endpoints.Count, Is.EqualTo(2), "Invalid number of on known endpoints");
        Assert.That(endpoints.Any(a => a.Name == "Endpoint1"), Is.True, "Should have found Endpoint1");
        Assert.That(endpoints.Any(a => a.Name == "Endpoint2"), Is.True, "Should have found Endpoint2");
    }

    [Test]
    public async Task Should_return_audit_remotes()
    {
        var remotes = await configuration.AuditQuery.GetAuditRemotes(default);

        Assert.That(remotes, Is.Not.Null, "Remotes should be found");
        Assert.That(remotes.Count, Is.EqualTo(1), "Invalid number of remotes");
        Assert.That(remotes[0].ApiUri, Is.EqualTo("http://localhost:44444/api/"), "Invalid ApiUri on remote");
        Assert.That(remotes[0].VersionString, Is.EqualTo("5.1.0"), "Invalid VersionString on remote");
        Assert.That(remotes[0].Retention, Is.EqualTo(new TimeSpan(7, 0, 0, 0)), "Invalid Retention on remote");
        Assert.That(remotes[0].Queues, Is.Not.Null, "Queues should be reported on remote");
        Assert.That(remotes[0].Queues.Count, Is.EqualTo(2), "Invalid number of queues reported on remote");
        Assert.That(remotes[0].Queues.Contains("audit"), Is.True, "Should have foind audit queue");
        Assert.That(remotes[0].Queues.Contains("audit.log"), Is.True, "Should have found audit.log queue");
    }

    [Test]
    public async Task Should_return_successful_audit_connection_if_instances_exist_and_are_online()
    {
        var connectionSettingsResult = await configuration.AuditQuery.TestAuditConnection(default);

        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
        Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0), "Unexpected ConnectionErrorMessages");
    }

    [Test]
    public async Task Should_return_correct_audit_count()
    {
        var auditCount = await configuration.AuditQuery.GetAuditCountForEndpoint("Endpoint1", default);

        Assert.That(auditCount, Is.Not.Null, "AuditCount should be returned");
        Assert.That(auditCount.Count, Is.EqualTo(3), "Invalid number of audit counts");
    }

    class ConfigurationApi_ReturningOneAuditConfigs : IConfigurationApi
    {
        public object GetConfig() => throw new NotImplementedException();

        public Task<object> GetRemoteConfigs(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateObject() as object);
        }

        object[] CreateObject()
        {
            object remote = new { ApiUri = "http://localhost:44444/api/", Status = "online", Version = "5.1.0", Configuration = JsonNode.Parse("{\"data_retention\":{ \"audit_retention_period\":\"7.00:00:00\"},\"transport\":{\"audit_log_queue\":\"audit.log\",\"audit_queue\":\"audit\" }}") };

            return [remote];
        }

        public RootUrls GetUrls(string baseUrl) => throw new NotImplementedException();
    }

    class EndpointsApi_ReturningTwoEndpoints : IEndpointsApi
    {
        public List<ServiceControl.Api.Contracts.Endpoint> GetEndpoints()
        {
            return [
                new ServiceControl.Api.Contracts.Endpoint { Id = Guid.NewGuid(), Name = "Endpoint1" },
                new ServiceControl.Api.Contracts.Endpoint { Id = Guid.NewGuid(), Name = "Endpoint2" }
            ];
        }

    }

    class AuditCountApi_ReturningThreeAuditCounts : IAuditCountApi
    {
        public async Task<IList<ServiceControl.Api.Contracts.AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken token)
        {
            var auditCounts = new List<ServiceControl.Api.Contracts.AuditCount>
            {
                new ServiceControl.Api.Contracts.AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-1).Date, Count = 5 },
                new ServiceControl.Api.Contracts.AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-2).Date, Count = 10 },
                new ServiceControl.Api.Contracts.AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-3).Date, Count = 15 }
            };

            return await Task.FromResult(auditCounts);
        }
    }
}