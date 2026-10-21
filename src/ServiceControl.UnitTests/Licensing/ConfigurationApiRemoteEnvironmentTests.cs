namespace ServiceControl.UnitTests.Licensing;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.ServiceControl.Licensing;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Api;
using ServiceControl.Monitoring.HeartbeatMonitoring;
using ServiceControl.UnitTests.ScatterGather;

[TestFixture]
class ConfigurationApiRemoteEnvironmentTests
{
    [Test]
    public async Task Reads_the_machine_hash_and_the_store_from_an_audit_response()
    {
        var remote = new RemoteInstanceSetting("http://audit.example:44444/api");
        var settings = new Settings { RemoteInstances = [remote] };
        var body = $$"""
            {
              "environment_data": { "Storage.Type": "SQLServer" },
              "machine_name_hash": "{{MachineIdentity.Hash}}",
              "storage_identity": { "engine": "SQLServer", "server_hash": "s", "database_hash": "d", "schema_hash": "x" }
            }
            """;
        var httpClientFactory = new FakeHttpClientFactory();
        httpClientFactory.Register(remote, (request, _) => Task.FromResult(request.RequestUri?.AbsolutePath == "/api/environment"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NotFound)));
        var api = new ConfigurationApi(new ActiveLicense(null, NullLogger<ActiveLicense>.Instance), settings, httpClientFactory, new MassTransitConnectorHeartbeatStatus(), [], NullLogger<ConfigurationApi>.Instance);

        var environment = (await api.GetRemoteEnvironments()).Single().EnvironmentData;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment, Does.ContainKey("Storage.Type").WithValue("SQLServer"));
            Assert.That(environment, Does.ContainKey("SameMachine").WithValue(MachineIdentity.Hash == MachineIdentity.NotApplicable ? "NotApplicable" : "True"));
            Assert.That(environment, Does.ContainKey(AuditEnvironmentMetadata.DatabaseKey).WithValue("s/d/x"));
        }
    }

    [Test]
    public async Task Logs_a_warning_and_reports_nothing_when_the_body_is_not_json()
    {
        var (api, logger) = Api(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>proxy error</html>", Encoding.UTF8, "text/html") });

        var environment = (await api.GetRemoteEnvironments()).Single().EnvironmentData;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment, Is.Null);
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
            Assert.That(logger.Warnings.Single().Message, Does.Contain(Remote.BaseAddress));
            Assert.That(logger.Warnings.Single().Exception, Is.Not.Null);
        }
    }

    [Test]
    public async Task Reports_nothing_rather_than_half_the_data_when_a_value_cannot_be_read()
    {
        var body = """
            {
              "environment_data": { "Storage.Type": "SQLServer", "Storage.SizeGB": 40 },
              "storage_identity": { "engine": "SQLServer", "server_hash": "s", "database_hash": "d", "schema_hash": "x" }
            }
            """;
        var (api, logger) = Api(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

        var environment = (await api.GetRemoteEnvironments()).Single().EnvironmentData;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment, Is.Null);
            Assert.That(logger.Warnings, Has.Count.EqualTo(1));
        }
    }

    [TestCase(HttpStatusCode.InternalServerError, 1)]
    [TestCase(HttpStatusCode.NotFound, 0)]
    public async Task Logs_a_warning_for_an_error_status_but_not_for_an_instance_without_the_endpoint(HttpStatusCode status, int expectedWarnings)
    {
        var (api, logger) = Api(_ => new HttpResponseMessage(status));

        var environment = (await api.GetRemoteEnvironments()).Single().EnvironmentData;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment, Is.Null);
            Assert.That(logger.Warnings, Has.Count.EqualTo(expectedWarnings));
        }
    }

    static readonly RemoteInstanceSetting Remote = new("http://audit.example:44444/api");

    static (ConfigurationApi Api, RecordingLogger Logger) Api(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClientFactory = new FakeHttpClientFactory();
        httpClientFactory.Register(Remote, (request, _) => Task.FromResult(respond(request)));
        var logger = new RecordingLogger();
        var api = new ConfigurationApi(new ActiveLicense(null, NullLogger<ActiveLicense>.Instance), new Settings { RemoteInstances = [Remote] }, httpClientFactory, new MassTransitConnectorHeartbeatStatus(), [], logger);

        return (api, logger);
    }

    class RecordingLogger : ILogger<ConfigurationApi>
    {
        public List<(string Message, Exception Exception)> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add((formatter(state, exception), exception));
            }
        }
    }
}
