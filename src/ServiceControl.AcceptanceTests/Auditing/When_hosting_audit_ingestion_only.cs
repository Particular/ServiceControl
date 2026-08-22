namespace ServiceControl.AcceptanceTests.Auditing
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.LicensingComponent.AuditThroughput;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;
    using ServiceControl.Hosting.Commands;
    using ServiceControl.Infrastructure;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Tests.AuditCapable;

    // The inner persistence type reaches the test persister through an environment variable, which is
    // process wide, so these cannot run alongside anything else that sets it.
    [NonParallelizable]
    class When_hosting_audit_ingestion_only : AcceptanceTest
    {
        [Test]
        public async Task Should_ingest_without_an_endpoint_and_without_the_single_owner_services()
        {
            var settings = await CreateSettings(auditCapable: true);

            var host = AuditIngestionOnlyCommand.BuildHost(settings);

            try
            {
                var hostedServices = host.Services.GetServices<IHostedService>()
                    .Select(hostedService => hostedService.GetType().Name)
                    .ToArray();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(host.Services.GetService<IMessageSession>(), Is.Null,
                        "the host must not run an NServiceBus endpoint");
                    Assert.That(host.Services.GetService<AuditIngestor>(), Is.Not.Null);

                    Assert.That(host.Services.GetService<IDatabaseMigrator>(), Is.Null,
                        "an ingestion only worker never changes the database schema");
                    Assert.That(host.Services.GetService<IBodyStorageInstaller>(), Is.Null,
                        "an ingestion only worker never provisions external storage");
                    Assert.That(host.Services.GetService<ILocalAuditSource>(), Is.Null,
                        "licensing throughput is owned by the normal primary, and would be counted once per node");
                    Assert.That(host.Services.GetService<ImportFailedAudits>(), Is.Not.Null);

                    Assert.That(hostedServices, Is.EquivalentTo(ExpectedHostedServices),
                        "the set of hosted services in the audit ingestion only host changed. Every one of "
                        + "these runs on every ingestion node, so decide whether that is safe before updating "
                        + "this list. Audit ingestion raises no domain events and no integration events, which "
                        + "is why EventLog and ExternalIntegrations are not registered.");
                }
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        [Test]
        public async Task Should_report_audit_ingestion_readiness()
        {
            var settings = await CreateSettings(auditCapable: true);

            var host = AuditIngestionOnlyCommand.BuildHost(settings);

            try
            {
                var readiness = host.Services.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

                var report = await readiness.CheckHealthAsync(registration => registration.Tags.Contains("ready"));

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(report.Entries.Keys, Does.Contain("audit-ingestion"));
                    Assert.That(report.Entries.Keys, Does.Not.Contain("error-ingestion"),
                        "this host does not ingest error messages, so it must not answer for them");
                }
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        [Test]
        public async Task Should_refuse_to_start_against_storage_without_audit_support()
        {
            var settings = await CreateSettings(auditCapable: false);

            var exception = Assert.ThrowsAsync<Exception>(() =>
                new AuditIngestionOnlyCommand().Execute(new HostArguments([]), settings));

            Assert.That(exception.Message, Does.Contain("supports audit ingestion"));
        }

        static readonly string[] ExpectedHostedServices =
        [
            "GenericWebHostService",                // health endpoints only, no ServiceControl API
            nameof(AuditIngestion),                 // the reason this host exists
            "HeartbeatMonitoringHostedService",     // warms the endpoint monitor, does not check heartbeats
            "InternalCustomChecksHostedService",    // reports this node's ingestion health to the database
            "MetricsReporterHostedService",
            "HealthCheckPublisherHostedService",     // inert, no IHealthCheckPublisher is registered
            "ExternalIntegrationRequestsDataStore"   // registered by the persister; its drain is inert here, nothing calls Subscribe
        ];

        [TearDown]
        public void ClearInnerPersistenceType() => Environment.SetEnvironmentVariable(InnerPersistenceTypeVariable, null);

        async Task<Settings> CreateSettings(bool auditCapable)
        {
            var persistenceType = StorageConfiguration.PersistenceType;

            if (auditCapable)
            {
                Environment.SetEnvironmentVariable(InnerPersistenceTypeVariable, persistenceType);
                persistenceType = AuditCapablePersistenceName;
            }

            var settings = new Settings(TransportIntegration.TypeName, persistenceType,
                CreateLoggingSettings(), forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = $"AuditIngestOnly.{Guid.NewGuid():n}",
                TransportConnectionString = TransportIntegration.ConnectionString,
                MaximumConcurrencyLevel = 2,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await StorageConfiguration.CustomizeSettings(settings);

            return settings;
        }

        static LoggingSettings CreateLoggingSettings()
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);
            return new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);
        }

        const string AuditCapablePersistenceName = "AuditCapableTest";

        static readonly string InnerPersistenceTypeVariable =
            AuditCapableTestPersistenceConfiguration.InnerPersistenceTypeSetting.ToUpperInvariant();
    }
}
