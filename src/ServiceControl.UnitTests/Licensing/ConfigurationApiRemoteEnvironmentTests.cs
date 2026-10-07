namespace ServiceControl.UnitTests.Licensing;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
        var api = new ConfigurationApi(new ActiveLicense(null, NullLogger<ActiveLicense>.Instance), settings, httpClientFactory, new MassTransitConnectorHeartbeatStatus(), []);

        var environment = (await api.GetRemoteEnvironments()).Single().EnvironmentData;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment, Does.ContainKey("Storage.Type").WithValue("SQLServer"));
            Assert.That(environment, Does.ContainKey("SameMachine").WithValue(MachineIdentity.Hash == MachineIdentity.NotApplicable ? "NotApplicable" : "True"));
            Assert.That(environment, Does.ContainKey(AuditEnvironmentMetadata.DatabaseKey).WithValue("s/d/x"));
        }
    }
}
