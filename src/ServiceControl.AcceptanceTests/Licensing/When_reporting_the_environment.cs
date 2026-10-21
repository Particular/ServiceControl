namespace ServiceControl.AcceptanceTests.Licensing
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.MonitoringThroughput;
    using Particular.LicensingComponent.Shared;
    using Recoverability.MessageRedirects;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_reporting_the_environment : AcceptanceTest
    {
        [Test]
        public async Task Should_describe_how_the_instance_is_deployed()
        {
            JsonDocument report = null;

            await Define<Context>()
                .WithEndpoint<MonitoringInstance>()
                .Do("Wait for the throughput data to be recorded", async _ =>
                {
                    var available = await this.TryGet<ReportGenerationState>(
                        "/api/licensing/report/available", state => state.ReportCanBeGenerated);

                    return available.HasResult;
                })
                .Do("Download the report", async _ =>
                {
                    var archive = await this.DownloadData("/api/licensing/report/file?spVersion=1.2.3");

                    report = ReadReport(archive);

                    return true;
                })
                .Done(_ => true)
                .Run();

            var data = report.RootElement
                .GetProperty("ReportData")
                .GetProperty("EnvironmentInformation")
                .GetProperty("EnvironmentData")
                .EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data.Keys, Is.SupersetOf(ExpectedKeys));

                Assert.That(data["Host.Model"], Is.AnyOf("Container", "WindowsService", "Console"));
                Assert.That(data["Storage.Type"], Is.Not.Empty);
                Assert.That(data["Storage.BodyStorage.Type"], Is.Not.Empty);
                Assert.That(data["Storage.HostingSource"], Is.AnyOf("Probe", "Configuration", "ConnectionString", "None"));
                Assert.That(data["Features.EmailNotifications"], Is.AnyOf("Enabled", "Disabled", "NotConfigured", "ReadFailed"));
                Assert.That(int.Parse(data["Retention.ErrorHours"]), Is.GreaterThan(0));

                Assert.That(data.Values, Has.None.Contains(Environment.MachineName),
                    "The report must not carry anything that identifies the customer's machine");
            }
        }

        static readonly string[] ExpectedKeys =
        [
            "Host.Model",
            "Host.Orchestrator",
            "Host.OSPlatform",
            "Host.OSVersion",
            "Host.Architecture",
            "Host.RuntimeVersion",
            "Host.ProcessorCount",
            "Host.AvailableMemoryGB",
            "Storage.Type",
            "Storage.Hosting",
            "Storage.ServerVersion",
            "Storage.HostingSource",
            "Storage.FullTextSearch",
            "Storage.BodyStorage.Type",
            "Storage.QueryTimeoutSeconds",
            "Storage.FreeSpaceThresholdPercent",
            "Transport.Type",
            "Features.IntegratedServicePulse",
            "Features.MessageEditing",
            "Features.ExternalIntegrationsPublishing",
            "Features.ForwardErrorMessages",
            "Features.ErrorIngestion",
            "Features.ConfigurationValidation",
            "Features.EmailNotifications",
            "ServicePulse.MonitoringUrl",
            "ServicePulse.DefaultRoute",
            "ServicePulse.ShowPendingRetry",
            "Host.VirtualDirectory",
            "Host.ShutdownTimeoutSeconds",
            "Logging.Providers",
            "Logging.Level",
            "Telemetry.OtlpMetrics",
            "Limits.ExternalIntegrationsBatchSize",
            "Ingestion.Error.MaxConcurrency",
            "Ingestion.Error.BatchSize",
            "Ingestion.Error.MaxParallelWriters",
            "Ingestion.Error.BatchTimeoutMs",
            "Ingestion.Error.RestartAfterFailureSeconds",
            "Heartbeats.TrackInstancesDefault",
            "Heartbeats.TrackInstancesOverrides",
            "Heartbeats.KnownInstances",
            "Heartbeats.MonitoredInstances",
            "Heartbeats.GracePeriodSeconds",
            "Recoverability.Redirects",
            "Recoverability.RetryHistoryDepth",
            "Licensing.ReportMasks",
            "Retention.ErrorHours",
            "Retention.EventsHours",
            "Health.Error.UptimeHours"
        ];

        [Test]
        public async Task Should_count_the_choices_made_in_servicepulse_without_revealing_them()
        {
            JsonDocument report = null;

            await Define<Context>()
                .WithEndpoint<MonitoringInstance>()
                .WithEndpoint<ScalingOut>()
                .Do("Wait for the first heartbeat of the endpoint that scales out", async _ =>
                {
                    var endpoints = await this.TryGetMany<EndpointsView>("/api/endpoints", endpoint => endpoint.Name == ScalingOutEndpoint && endpoint.Monitored);

                    return endpoints.HasResult;
                })
                .Do("Stop tracking instances by default", async _ =>
                {
                    await this.Patch("/api/endpointssettings", new { track_instances = false });

                    var settings = await this.TryGetMany<SettingsData>("/api/endpointssettings",
                        setting => setting.Name == string.Empty && !setting.TrackInstances);

                    return settings.HasResult;
                })
                .Do("Keep tracking the endpoint that scales out", async _ =>
                {
                    await this.Patch($"/api/endpointssettings/{ScalingOutEndpoint}", new { track_instances = true });

                    var settings = await this.TryGetMany<SettingsData>("/api/endpointssettings",
                        setting => setting.Name == ScalingOutEndpoint && setting.TrackInstances);

                    return settings.HasResult;
                })
                .Do("Redirect a retired queue", async _ =>
                {
                    await this.Post("/api/redirects", new RedirectRequest { fromphysicaladdress = RetiredQueue, tophysicaladdress = ReplacementQueue });

                    var redirects = await this.TryGetMany<MessageRedirectFromJson>("/api/redirects");

                    return redirects.HasResult;
                })
                .Do("Mask the customer's name in the report", async _ =>
                {
                    await this.Post("/api/licensing/settings/masks/update", new[] { MaskedWord });

                    return true;
                })
                .Do("Wait for the throughput data to be recorded", async _ =>
                {
                    var available = await this.TryGet<ReportGenerationState>(
                        "/api/licensing/report/available", state => state.ReportCanBeGenerated);

                    return available.HasResult;
                })
                .Do("Download the report", async _ =>
                {
                    var archive = await this.DownloadData("/api/licensing/report/file?spVersion=1.2.3");

                    report = ReadReport(archive);

                    return true;
                })
                .Done(_ => true)
                .Run();

            var data = report.RootElement
                .GetProperty("ReportData")
                .GetProperty("EnvironmentInformation")
                .GetProperty("EnvironmentData")
                .EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data["Heartbeats.TrackInstancesDefault"], Is.EqualTo("Disabled"));
                Assert.That(data["Heartbeats.TrackInstancesOverrides"], Is.EqualTo("1"));
                Assert.That(int.Parse(data["Heartbeats.MonitoredInstances"]), Is.GreaterThanOrEqualTo(1));
                Assert.That(int.Parse(data["Heartbeats.KnownInstances"]), Is.GreaterThanOrEqualTo(int.Parse(data["Heartbeats.MonitoredInstances"])));
                Assert.That(data["Recoverability.Redirects"], Is.EqualTo("1"));
                Assert.That(data["Licensing.ReportMasks"], Is.EqualTo("1"));
                Assert.That(string.Join("|", data.Values), Does.Not.Contain(RetiredQueue).And.Not.Contain(ReplacementQueue).And.Not.Contain(MaskedWord).And.Not.Contain(ScalingOutEndpoint));
            }
        }

        static JsonDocument ReadReport(byte[] archive)
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            using var entry = zip.Entries.Single().Open();

            return JsonDocument.Parse(entry);
        }

        const string SalesEndpoint = "Particular.Sales";
        const string RetiredQueue = "Contoso.Billing.Legacy@machine1";
        const string ReplacementQueue = "Contoso.Billing@machine2";
        const string MaskedWord = "Contoso";

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
        }

        static string ScalingOutEndpoint => Conventions.EndpointNamingConvention(typeof(ScalingOut));

        class ScalingOut : EndpointConfigurationBuilder
        {
            public ScalingOut() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.SendHeartbeatTo(Settings.DEFAULT_INSTANCE_NAME));
        }

        class MonitoringInstance : EndpointConfigurationBuilder
        {
            public MonitoringInstance() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<ReportThroughput>());

            class ReportThroughput : DispatchRawMessages<Context>
            {
                protected override TransportOperations CreateMessage(Context context)
                {
                    var recorded = new RecordEndpointThroughputData
                    {
                        StartDateTime = DateTime.UtcNow.AddDays(-1).AddHours(-1),
                        EndDateTime = DateTime.UtcNow.AddDays(-1),
                        EndpointThroughputData = [new EndpointThroughputData { Name = SalesEndpoint, Throughput = 42 }]
                    };

                    var body = JsonSerializer.SerializeToUtf8Bytes(recorded);
                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], body);

                    return new TransportOperations(
                        new TransportOperation(message, new UnicastAddressTag(ServiceControlSettings.ServiceControlThroughputDataQueue)));
                }
            }
        }
    }
}
