namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Particular.Approvals;
using Particular.LicensingComponent.AuditThroughput;
using Particular.LicensingComponent.UnitTests.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;
using AuditCount = ServiceControl.Api.Contracts.AuditCount;
using Endpoint = ServiceControl.Api.Contracts.Endpoint;

[TestFixture]
class AuditQuery_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_return_known_endpoints_if_any()
    {
        //Arrange
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new EndpointsApi_ReturningTwoEndpoints(), new FakeAuditCountApi(), new FakeConfigurationApi());

        //Act
        var endpoints = (await auditQuery.GetKnownEndpoints(default)).ToList();

        //Assert
        Assert.That(endpoints, Is.Not.Null, "Endpoints should be found");
        Assert.That(endpoints.Count, Is.EqualTo(2), "Invalid number of on known endpoints");
        Assert.Multiple(() =>
        {
            Assert.That(endpoints.Any(a => a.Name == "Endpoint1"), Is.True, "Should have found Endpoint1");
            Assert.That(endpoints.Any(a => a.Name == "Endpoint2"), Is.True, "Should have found Endpoint2");
        });
    }

    [Test]
    public async Task Should_return_audit_remotes()
    {
        //Arrange
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new FakeEndpointApi(), new FakeAuditCountApi(), new ConfigurationApi_ReturningOneValidAuditConfig());

        //Act
        var remotes = await auditQuery.GetAuditRemotes(default);

        //Assert
        Assert.That(remotes, Is.Not.Null, "Remotes should be found");
        Assert.That(remotes, Has.Count.EqualTo(1), "Invalid number of remotes");
        Assert.Multiple(() =>
        {
            Assert.That(remotes[0].ApiUri, Is.EqualTo("http://localhost:44444/api/"), "Invalid ApiUri on remote");
            Assert.That(remotes[0].VersionString, Is.EqualTo("5.1.0"), "Invalid VersionString on remote");
            Assert.That(remotes[0].Retention, Is.EqualTo(new TimeSpan(7, 0, 0, 0)), "Invalid Retention on remote");
            Assert.That(remotes[0].Queues, Is.Not.Null, "Queues should be reported on remote");
        });

        Assert.That(remotes[0].Queues, Has.Count.EqualTo(2), "Invalid number of queues reported on remote");
        Assert.Multiple(() =>
        {
            Assert.That(remotes[0].Queues, Does.Contain("audit"), "Should have foind audit queue");
            Assert.That(remotes[0].Queues, Does.Contain("audit.log"), "Should have found audit.log queue");
        });
    }

    [Test]
    public async Task Should_return_successful_audit_connection_if_instances_exist_and_are_online()
    {
        //Arrange
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new FakeEndpointApi(), new FakeAuditCountApi(), new ConfigurationApi_ReturningOneValidAuditConfig());

        //Act
        var connectionSettingsResult = await auditQuery.TestAuditConnection(default);

        //Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.Multiple(() =>
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0), "Unexpected ConnectionErrorMessages");
        });

        Approver.Verify(connectionSettingsResult.Diagnostics);
    }


    [Test]
    public async Task Should_return_diagnostics_and_no_errors_when_no_remotes_defined()
    {
        //Arrange
        var confiApi = new ConfigurationApi_Configurable() { ReturnAuditConfig = false };
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new FakeEndpointApi(), new FakeAuditCountApi(), confiApi);

        //Act
        var connectionSettingsResult = await auditQuery.TestAuditConnection(default);

        //Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.Multiple(() =>
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.True, "Connection status should be successful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.EqualTo(0), "Unexpected ConnectionErrorMessages");
        });

        Approver.Verify(connectionSettingsResult.Diagnostics);
    }

    [TestCase("unavailable", null, "7.00:00:00")]
    [TestCase("error", null, "7.00:00:00")]
    [TestCase("online", null, "7.00:00:00")]
    [TestCase("online", "3.2.0", "7.00:00:00")]
    [TestCase("online", "5.1.0", "1.00:00:00")]
    public async Task Should_always_return_diagnostics_and_relevant_errors_when_invalid_remotes_exist(string remoteStatus, string remoteVersion, string retensionPeriod)
    {
        //Arrange
        var confiApi = new ConfigurationApi_Configurable() { ReturnAuditConfig = true, RemoteStatus = remoteStatus, RemoteVersion = remoteVersion, AuditRetensionPeriod = retensionPeriod };
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new FakeEndpointApi(), new FakeAuditCountApi(), confiApi);

        //Act
        var connectionSettingsResult = await auditQuery.TestAuditConnection(default);

        //Assert
        Assert.That(connectionSettingsResult, Is.Not.Null, "connectionSettingsResult should be returned");
        Assert.Multiple(() =>
        {
            Assert.That(connectionSettingsResult.ConnectionSuccessful, Is.False, "Connection status should not be successful");
            Assert.That(connectionSettingsResult.ConnectionErrorMessages.Count, Is.Not.EqualTo(0), "Expected ConnectionErrorMessages");
        });

        Approver.Verify(connectionSettingsResult.Diagnostics, scenario: $"status_{remoteStatus}.version_{remoteVersion?.Replace(".", "") ?? "null"}.retention_{retensionPeriod[0]}");
    }

    [Test]
    public async Task Should_return_correct_audit_count()
    {
        //Arrange
        var auditQuery = new AuditQuery(NullLogger<AuditQuery>.Instance, new FakeEndpointApi(), new AuditCountApi_ReturningThreeAuditCounts(), new FakeConfigurationApi());

        //Act
        var auditCount = await auditQuery.GetAuditCountForEndpoint("Endpoint1", default);

        Assert.That(auditCount, Is.Not.Null, "AuditCount should be returned");
        Assert.That(auditCount.Count, Is.EqualTo(3), "Invalid number of audit counts");
    }

    class ConfigurationApi_ReturningOneValidAuditConfig : IConfigurationApi
    {
        public Task<object> GetConfig(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default)
        {
            var remote = new RemoteConfiguration { ApiUri = "http://localhost:44444/api/", Status = "online", Version = "5.1.0", Configuration = JsonNode.Parse("{\"data_retention\":{ \"audit_retention_period\":\"7.00:00:00\"},\"transport\":{\"audit_log_queue\":\"audit.log\",\"audit_queue\":\"audit\" }}") };

            return Task.FromResult<RemoteConfiguration[]>([remote]);
        }

        public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    class ConfigurationApi_Configurable : IConfigurationApi
    {
        public Task<object> GetConfig(CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default)
        {
            if (!ReturnAuditConfig)
            {
                return default;
            }

            var remote = new RemoteConfiguration { ApiUri = "http://localhost:44444/api/", Status = RemoteStatus, Version = RemoteVersion, Configuration = JsonNode.Parse("{\"data_retention\":{ \"audit_retention_period\":\"" + AuditRetensionPeriod + "\"},\"transport\":{\"audit_log_queue\":\"audit.log\",\"audit_queue\":\"audit\" }}") };

            return Task.FromResult<RemoteConfiguration[]>([remote]);
        }

        public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken) => throw new NotImplementedException();

        public bool ReturnAuditConfig { get; set; }
        public string RemoteStatus { get; set; }
        public string RemoteVersion { get; set; }
        public string AuditRetensionPeriod { get; set; }
    }

    class EndpointsApi_ReturningTwoEndpoints : IEndpointsApi
    {
        public Task<List<Endpoint>> GetEndpoints(CancellationToken cancellationToken)
        {
            return Task.FromResult<List<Endpoint>>([
                new Endpoint { Id = Guid.NewGuid(), Name = "Endpoint1" },
                new Endpoint { Id = Guid.NewGuid(), Name = "Endpoint2" }
            ]);
        }

    }

    class AuditCountApi_ReturningThreeAuditCounts : IAuditCountApi
    {
        public async Task<IList<AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken token)
        {
            var auditCounts = new List<AuditCount>
            {
                new AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-1).Date, Count = 5 },
                new AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-2).Date, Count = 10 },
                new AuditCount() { UtcDate = DateTime.UtcNow.AddDays(-3).Date, Count = 15 }
            };

            return await Task.FromResult(auditCounts);
        }
    }

}