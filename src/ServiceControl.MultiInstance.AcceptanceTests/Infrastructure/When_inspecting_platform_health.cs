namespace ServiceControl.MultiInstance.AcceptanceTests.Infrastructure;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using Api.Contracts;
using Audit.Auditing;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using TestSupport;

class When_inspecting_platform_health : AcceptanceTest
{
    [Test]
    public async Task Should_show_audit_issues_recovery_and_unavailable_configured_instances()
    {
        const string checkId = "Audit Message Ingestion Process";
        const string failure = "Audit ingestion interrupted for the platform health scenario";
        var auditState = new AuditIngestionCustomCheck.State();
        auditState.ReportError(failure);
        var offline = new RemoteInstanceSetting("http://offline:12121");

        AuditHostBuilderCustomization = builder => builder.Services.AddSingleton(auditState);
        CustomServiceControlPrimarySettings = settings => settings.RemoteInstances = [.. settings.RemoteInstances, offline];
        PrimaryHostBuilderCustomization = builder => builder.Services.AddKeyedSingleton<Func<HttpMessageHandler>>(
            offline.InstanceId, () => new UnavailableHandler());

        PlatformHealthView failing = null;
        PlatformHealthView recovered = null;

        await Define<Context>()
            .Do("Read the platform inventory and failing audit report", async context =>
            {
                failing = await this.TryGet<PlatformHealthView>("/api/platform-health", instanceName: ServiceControlInstanceName);
                context.LastAlerts = string.Join(", ", failing?.Alerts.Select(alert => $"{alert.InstanceName}: {alert.CheckId}") ?? []);
                return failing?.Alerts.Any(alert => alert.CheckId == checkId) == true;
            })
            .Do("Observe audit recovery in platform health", async context =>
            {
                if (!context.RecoveryRequested)
                {
                    auditState.Clear();
                    context.RecoveryRequested = true;
                }

                recovered = await this.TryGet<PlatformHealthView>("/api/platform-health", instanceName: ServiceControlInstanceName);
                context.LastAlerts = string.Join(", ", recovered?.Alerts.Select(alert => $"{alert.InstanceName}: {alert.CheckId}") ?? []);
                return recovered != null && recovered.Alerts.All(alert => alert.CheckId != checkId);
            })
            .Done(_ => true)
            .Run();

        var primary = failing.Instances.Single(instance => instance.Role == "primary-error");
        var audit = failing.Instances.Single(instance => instance.Role == "remote-audit");
        var unavailable = failing.Instances.Single(instance => instance.Id == offline.InstanceId);
        var issue = failing.Alerts.Single(alert => alert.CheckId == checkId);
        var recoveredAudit = recovered.Instances.Single(instance => instance.Id == audit.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failing.Instances, Has.Length.EqualTo(3));
            Assert.That(primary.Name, Is.EqualTo(ServiceControlInstanceName));
            Assert.That(primary.Issues, Has.None.Matches<PlatformHealthAlert>(alert => alert.CheckId == checkId));
            Assert.That(audit.Name, Is.EqualTo(ServiceControlAuditInstanceName));
            Assert.That(audit.HostId, Is.EqualTo(issue.HostId));
            Assert.That(audit.Health, Is.EqualTo("degraded"));
            Assert.That(audit.HealthSignalsStatus, Is.EqualTo("reported"));
            Assert.That(audit.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(audit.AuditRetentionPeriod, Is.Not.Null);
            Assert.That(audit.Issues, Has.Some.Matches<PlatformHealthAlert>(alert => alert.Id == issue.Id));
            Assert.That(issue.InstanceId, Is.EqualTo(audit.Id));
            Assert.That(issue.Message, Is.EqualTo(failure));
            Assert.That(unavailable.Kind, Is.EqualTo("unknown"));
            Assert.That(unavailable.Health, Is.EqualTo("unavailable"));
            Assert.That(unavailable.Version, Is.Null);
            Assert.That(recoveredAudit.Health, Is.EqualTo("healthy"));
            Assert.That(recoveredAudit.Issues, Is.Empty);
        }
    }

    sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException(HttpRequestError.ConnectionError);
    }

    class Context : ScenarioContext, ISequenceContext
    {
        public int Step { get; set; }
        public string LastAlerts { get; set; }
        public bool RecoveryRequested { get; set; }
    }
}