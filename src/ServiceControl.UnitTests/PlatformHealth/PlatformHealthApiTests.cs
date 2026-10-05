namespace ServiceControl.UnitTests.PlatformHealth;

using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Hosting;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Infrastructure;
using ServiceControl.Licensing;
using ServiceControl.Monitoring.HeartbeatMonitoring;
using ServiceControl.Operations;
using ServiceControl.PlatformHealth;

[TestFixture]
class PlatformHealthApiTests
{
    [SetUp]
    public void SetUp()
    {
        settings = new Settings
        {
            InstanceName = "Primary",
            TransportType = "RabbitMQ",
            ErrorQueue = "error",
            ErrorLogQueue = "error.log",
            ForwardErrorMessages = false,
            RemoteInstances = []
        };
        state = new PlatformHealthState();
        configuration = new FakeConfigurationApi();
        licensing = new FakeLicenseInfoProvider();
        clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        api = new PlatformHealthApi(settings, new HostInformation(PrimaryHostId, "primary-host"), state,
            configuration, licensing, new MassTransitConnectorHeartbeatStatus(), clock);
    }

    [Test]
    public async Task Includes_primary_inventory_configuration_and_version_before_checks_report()
    {
        var result = await api.GetHealth("https://public/servicecontrol/api");
        var primary = result.Instances.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo("unknown"));
            Assert.That(primary.Id, Is.EqualTo(settings.InstanceId));
            Assert.That(primary.Name, Is.EqualTo("Primary"));
            Assert.That(primary.Kind, Is.EqualTo("error"));
            Assert.That(primary.Role, Is.EqualTo("primary-error"));
            Assert.That(primary.ApiUrl, Is.EqualTo("https://public/servicecontrol/api/"));
            Assert.That(primary.Version, Is.EqualTo(ServiceControlVersion.GetFileVersion()));
            Assert.That(primary.Health, Is.EqualTo("healthy"));
            Assert.That(primary.HealthSignalsStatus, Is.EqualTo("unreported"));
            Assert.That(primary.LastReportedAt, Is.Null);
            Assert.That(primary.ObservedAt, Is.EqualTo(clock.GetUtcNow()));
            Assert.That(primary.HostId, Is.EqualTo(PrimaryHostId));
            Assert.That(primary.TransportType, Is.EqualTo("RabbitMQ"));
            Assert.That(primary.ErrorQueue, Is.EqualTo("error"));
            Assert.That(primary.ErrorLogQueue, Is.EqualTo("error.log"));
            Assert.That(primary.ForwardErrorMessages, Is.False);
            Assert.That(primary.ErrorRetentionPeriod, Is.EqualTo(settings.ErrorRetentionPeriod));
            Assert.That(licensing.Refresh, Is.True);
            Assert.That(licensing.ClientName, Is.EqualTo("servicepulse"));
        }
    }

    [Test]
    public async Task Controller_uses_the_public_scheme_host_and_proxy_prefix()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("public.example", 8443);
        context.Request.PathBase = "/servicecontrol";
        var controller = new PlatformHealthController(api) { ControllerContext = new ControllerContext { HttpContext = context } };

        var result = await controller.Get();

        Assert.That(result.Instances[0].ApiUrl, Is.EqualTo("https://public.example:8443/servicecontrol/api/"));
    }

    [Test]
    public async Task Correlates_issues_by_host_identity_and_clears_them_after_recovery()
    {
        var remote = new RemoteInstanceSetting("https://audit");
        settings.RemoteInstances = [remote];
        configuration.Remotes = [Remote(remote, "Primary", AuditHostId)];
        var report = Report("Primary", AuditHostId);
        state.Record(report);

        var failing = await api.GetHealth("https://primary/api/");
        var audit = failing.Instances.Single(instance => instance.Id == remote.InstanceId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failing.Instances[0].Issues, Is.Empty);
            Assert.That(audit.Kind, Is.EqualTo("audit"));
            Assert.That(audit.Role, Is.EqualTo("remote-audit"));
            Assert.That(audit.Health, Is.EqualTo("degraded"));
            Assert.That(audit.HealthSignalsStatus, Is.EqualTo("reported"));
            Assert.That(audit.AuditRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(audit.Issues, Has.Length.EqualTo(1));
            Assert.That(audit.Issues[0].InstanceId, Is.EqualTo(remote.InstanceId));
            Assert.That(failing.Alerts[0].InstanceId, Is.EqualTo(remote.InstanceId));
        }

        report.HasFailed = false;
        report.ReportedAt = report.ReportedAt.AddMinutes(1);
        state.Record(report);
        var recovered = await api.GetHealth("https://primary/api/");

        Assert.That(recovered.Instances.Single(instance => instance.Id == remote.InstanceId).Health, Is.EqualTo("healthy"));
        Assert.That(recovered.Alerts, Is.Empty);
    }

    [Test]
    public async Task Keeps_configured_offline_instances_and_last_observed_metadata()
    {
        var remote = new RemoteInstanceSetting("https://audit/prefix");
        settings.RemoteInstances = [remote];
        var first = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Instances, Has.Length.EqualTo(2));
            Assert.That(first.Instances[1].Health, Is.EqualTo("unavailable"));
            Assert.That(first.Instances[1].Kind, Is.EqualTo("unknown"));
            Assert.That(first.Instances[1].Version, Is.Null);
        }

        configuration.Remotes = [Remote(remote, "Audit", AuditHostId)];
        var online = await api.GetHealth("https://primary/api/");
        configuration.Remotes = [];
        clock.Advance(TimeSpan.FromMinutes(1));
        var offline = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(offline.Instances[1].Name, Is.EqualTo("Audit"));
            Assert.That(offline.Instances[1].Kind, Is.EqualTo("audit"));
            Assert.That(offline.Instances[1].Version, Is.EqualTo("6.10.0"));
            Assert.That(offline.Instances[1].ApiUrl, Is.EqualTo("https://audit/prefix/api/"));
            Assert.That(offline.Instances[1].Health, Is.EqualTo("unavailable"));
            Assert.That(offline.Instances[1].MetadataObservedAt, Is.EqualTo(online.Instances[1].ObservedAt));
            Assert.That(offline.Instances[1].ObservedAt, Is.EqualTo(clock.GetUtcNow()));
        }
    }

    [Test]
    public async Task Remote_error_instances_keep_configuration_and_recover_connectivity()
    {
        var remote = new RemoteInstanceSetting("https://remote-error/prefix");
        settings.RemoteInstances = [remote];
        var metadata = Remote(remote, "Remote error", AuditHostId);
        metadata.Configuration["instance_type"] = "error";
        metadata.Configuration["data_retention"] = JsonNode.Parse("""{"error_retention_period":"21.00:00:00"}""");
        metadata.Configuration["transport"] = JsonNode.Parse("""
            {"transport_type":"RabbitMQ","error_queue":"remote.error","error_log_queue":"remote.log","forward_error_messages":false}
            """);
        configuration.Remotes = [metadata];
        state.Record(Report("Remote error", AuditHostId));

        var result = await api.GetHealth("https://primary/api/");
        var instance = result.Instances[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Kind, Is.EqualTo("error"));
            Assert.That(instance.Role, Is.EqualTo("remote-error"));
            Assert.That(instance.Health, Is.EqualTo("degraded"));
            Assert.That(instance.ErrorQueue, Is.EqualTo("remote.error"));
            Assert.That(instance.ErrorLogQueue, Is.EqualTo("remote.log"));
            Assert.That(instance.ForwardErrorMessages, Is.False);
            Assert.That(instance.ErrorRetentionPeriod, Is.EqualTo(TimeSpan.FromDays(21)));
            Assert.That(instance.AuditRetentionPeriod, Is.Null);
        }

        metadata.Status = "unavailable";
        Assert.That((await api.GetHealth("https://primary/api/")).Instances[1].Health, Is.EqualTo("unavailable"));
        metadata.Status = "online";
        metadata.Version = "6.11.0";
        clock.Advance(TimeSpan.FromMinutes(1));
        var recovered = (await api.GetHealth("https://primary/api/")).Instances[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered.Health, Is.EqualTo("degraded"));
            Assert.That(recovered.Version, Is.EqualTo("6.11.0"));
            Assert.That(recovered.MetadataObservedAt, Is.EqualTo(clock.GetUtcNow()));
        }
    }

    [Test]
    public async Task Malformed_optional_remote_metadata_does_not_hide_other_instances()
    {
        var malformed = new RemoteInstanceSetting("https://malformed");
        var valid = new RemoteInstanceSetting("https://valid");
        settings.RemoteInstances = [malformed, valid];
        var badMetadata = Remote(malformed, "Malformed audit", AuditHostId);
        badMetadata.Configuration["data_retention"]["audit_retention_period"] = "not a duration";
        configuration.Remotes = [badMetadata, Remote(valid, "Valid audit", Guid.NewGuid())];

        var result = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Instances, Has.Length.EqualTo(3));
            Assert.That(result.Instances.Single(instance => instance.Id == malformed.InstanceId).Health, Is.EqualTo("unavailable"));
            Assert.That(result.Instances.Single(instance => instance.Id == valid.InstanceId).Health, Is.EqualTo("healthy"));
            Assert.That(result.License.Availability, Is.EqualTo("available"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Legacy_name_matching_requires_unique_inventory_and_reporting_host(bool duplicateInventory)
    {
        var first = new RemoteInstanceSetting("https://first");
        var second = new RemoteInstanceSetting("https://second");
        settings.RemoteInstances = duplicateInventory ? [first, second] : [first];
        configuration.Remotes = duplicateInventory ? [Remote(first, "Audit"), Remote(second, "Audit")] : [Remote(first, "Audit")];
        state.Record(Report("Audit", AuditHostId));
        if (!duplicateInventory)
        {
            state.Record(Report("Audit", Guid.NewGuid()));
        }

        var result = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Instances.Skip(1).Select(instance => instance.HealthSignalsStatus), Is.All.EqualTo("ambiguous"));
            Assert.That(result.Instances.SelectMany(instance => instance.Issues), Is.Empty);
            Assert.That(result.Alerts.Select(alert => alert.InstanceId), Is.All.Null);
        }
    }

    [Test]
    public async Task Legacy_configuration_infers_kind_and_matches_unique_names_without_inventing_versions()
    {
        var remote = new RemoteInstanceSetting("https://audit");
        settings.RemoteInstances = [remote];
        var legacy = Remote(remote, "Audit");
        legacy.Configuration.AsObject().Remove("instance_type");
        legacy.Version = "Unknown";
        configuration.Remotes = [legacy];
        state.Record(Report("audit", AuditHostId));

        var result = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Instances[1].Kind, Is.EqualTo("audit"));
            Assert.That(result.Instances[1].Version, Is.Null);
            Assert.That(result.Instances[1].Health, Is.EqualTo("degraded"));
            Assert.That(result.Alerts[0].InstanceId, Is.EqualTo(remote.InstanceId));
        }
    }

    [Test]
    public async Task License_failure_and_remote_failure_do_not_hide_local_health()
    {
        settings.RemoteInstances = [new RemoteInstanceSetting("https://offline")];
        configuration.Failure = new InvalidOperationException("Remote unavailable");
        licensing.Failure = new InvalidOperationException("License unavailable");
        state.Record(Report("Primary", PrimaryHostId));

        var result = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Instances, Has.Length.EqualTo(2));
            Assert.That(result.Instances[0].Health, Is.EqualTo("degraded"));
            Assert.That(result.Instances[1].Health, Is.EqualTo("unavailable"));
            Assert.That(result.License.Availability, Is.EqualTo("unavailable"));
            Assert.That(result.License.Status, Is.Null);
            Assert.That(result.License.LicenseStatus, Is.Null);
        }
    }

    [TestCase("Valid")]
    [TestCase("ValidWithExpiringTrial")]
    [TestCase("InvalidDueToExpiredTrial")]
    [TestCase("InvalidDueToExpiredSubscription")]
    [TestCase("InvalidDueToExpiredUpgradeProtection")]
    public async Task License_status_and_renewal_fields_preserve_the_license_api_values(string licenseStatus)
    {
        licensing.Info.LicenseStatus = licenseStatus;
        licensing.Info.ExpirationDate = "2026-10-01T00:00:00.0000000Z";
        licensing.Info.UpgradeProtectionExpiration = "";

        var result = await api.GetHealth("https://primary/api/");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.License.Availability, Is.EqualTo("available"));
            Assert.That(result.License.LicenseStatus, Is.EqualTo(licenseStatus));
            Assert.That(result.License.LicenseType, Is.EqualTo(licensing.Info.LicenseType));
            Assert.That(result.License.TrialLicense, Is.EqualTo(licensing.Info.TrialLicense));
            Assert.That(result.License.LicenseExtensionUrl, Is.EqualTo(licensing.Info.LicenseExtensionUrl));
            Assert.That(result.License.ExpirationDate, Is.EqualTo(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(result.License.UpgradeProtectionExpiration, Is.Null);
        }
    }

    [Test]
    public async Task Disabled_checks_are_explicit_and_not_mistaken_for_reports()
    {
        settings.DisableHealthChecks = true;

        var result = await api.GetHealth("https://primary/api/");

        Assert.That(result.Instances[0].HealthSignalsStatus, Is.EqualTo("disabled"));
    }

    [Test]
    public void Caller_cancellation_is_not_converted_to_partial_success()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.That(async () => await api.GetHealth("https://primary/api/", cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void Cancellation_is_forwarded_to_dependencies_and_propagated_during_refresh()
    {
        settings.RemoteInstances = [new RemoteInstanceSetting("https://audit")];
        using var cancellation = new CancellationTokenSource();
        licensing.BeforeRead = cancellation.Cancel;
        licensing.Failure = new OperationCanceledException(cancellation.Token);

        Assert.That(async () => await api.GetHealth("https://primary/api/", cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.CancellationToken, Is.EqualTo(cancellation.Token));
            Assert.That(licensing.CancellationToken, Is.EqualTo(cancellation.Token));
        }
    }

    static RemoteConfiguration Remote(RemoteInstanceSetting setting, string name, Guid? hostId = null)
    {
        var configuration = JsonNode.Parse("""
            {"instance_type":"audit","host":{},"data_retention":{"audit_retention_period":"7.00:00:00"}}
            """);
        configuration["host"]["instance_name"] = name;
        if (hostId.HasValue)
        {
            configuration["host"]["host_id"] = hostId.Value.ToString();
        }
        return new RemoteConfiguration { ApiUri = setting.BaseAddress, Configuration = configuration, Status = "online", Version = "6.10.0" };
    }

    CustomCheckDetail Report(string name, Guid hostId) => new()
    {
        CustomCheckId = "Audit Message Ingestion",
        Category = "ServiceControl Health",
        HasFailed = true,
        FailureReason = "Ingestion failed",
        ReportedAt = clock.GetUtcNow().UtcDateTime,
        OriginatingEndpoint = new EndpointDetails { Name = name, Host = "host", HostId = hostId }
    };

    sealed class FakeConfigurationApi : IConfigurationApi
    {
        public RemoteConfiguration[] Remotes { get; set; } = [];
        public Exception Failure { get; set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return Failure == null ? Task.FromResult(Remotes) : Task.FromException<RemoteConfiguration[]>(Failure);
        }

        public Task<object> GetConfig(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class FakeLicenseInfoProvider : ILicenseInfoProvider
    {
        public LicenseInfo Info { get; } = new()
        {
            Status = "valid",
            LicenseStatus = "Valid",
            LicenseType = "Trial",
            TrialLicense = true,
            LicenseExtensionUrl = "https://particular.net/extend-your-trial?p=servicepulse"
        };
        public Exception Failure { get; set; }
        public bool Refresh { get; private set; }
        public string ClientName { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Action BeforeRead { get; set; }

        public Task<LicenseInfo> GetLicense(bool refresh, string clientName, CancellationToken cancellationToken = default)
        {
            Refresh = refresh;
            ClientName = clientName;
            CancellationToken = cancellationToken;
            BeforeRead?.Invoke();
            return Failure == null ? Task.FromResult(Info) : Task.FromException<LicenseInfo>(Failure);
        }
    }

    Settings settings;
    PlatformHealthState state;
    FakeConfigurationApi configuration;
    FakeLicenseInfoProvider licensing;
    FakeTimeProvider clock;
    PlatformHealthApi api;
    static readonly Guid PrimaryHostId = Guid.Parse("BD444A23-93E3-42E5-A9C2-15CD7436756E");
    static readonly Guid AuditHostId = Guid.Parse("627A66F4-F7C5-4D18-8793-0D8C385A5744");
}