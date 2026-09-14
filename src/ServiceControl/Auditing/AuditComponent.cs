namespace ServiceControl.Auditing
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Particular.LicensingComponent.AuditThroughput;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing.Metrics;
    using ServiceControl.Connection;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.Health;
    using ServiceControl.Persistence;
    using ServiceControl.Transports;

    // Registers nothing unless the configured persister advertises audit support in its manifest, so
    // hosts on a persister that cannot store audit data behave exactly as they did before.
    class AuditComponent : ServiceControlComponent
    {
        public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
        {
            if (!HostsAuditData(settings))
            {
                return;
            }

            context.CreateQueue(settings.AuditQueue);

            if (settings.ForwardAuditMessages && settings.AuditLogQueue != null)
            {
                context.CreateQueue(settings.AuditLogQueue);
            }
        }

        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            if (!HostsAuditData(settings))
            {
                return;
            }

            EnsureRemotesAreNotCombinedWithLocalIngestion(settings);

            var services = hostBuilder.Services;

            services.TryAddSingleton<IEndpointDetectionReporter, NoEndpointDetectionReporter>();

            services.AddSingleton<AuditIngestionMetrics>();
            services.AddSingleton<AuditIngestor>();
            services.AddSingleton<ImportFailedAudits>();
            services.AddSingleton<AuditIngestionCustomCheck.State>();

            services.AddCustomCheck<AuditIngestionCustomCheck>();
            services.AddCustomCheck<FailedAuditImportCustomCheck>();

            services.AddHealthChecks()
                .AddCheck<AuditIngestionHealthCheck>("audit-ingestion", tags: [HealthCheckExtensions.ReadyTag]);

            if (settings.IngestAuditMessages)
            {
                services.AddHostedService<AuditIngestion>();
            }

            if (settings.Host.HostsApi)
            {
                // Registered before the licensing component's own fallback, which uses TryAdd.
                services.AddSingleton<ILocalAuditSource, PrimaryLocalAuditSource>();
                services.AddPlatformConnectionProvider<AuditPlatformConnectionDetailsProvider>();
            }
        }

        // A primary that ingests audit itself and also lists audit remotes is one of two mistakes:
        // either the remotes are left over from before audit moved into this database, or the audit
        // data was meant to be Remote and this instance would ingest a queue that belongs to the
        // audit host. Both read the same setting names, so a shared environment file also makes the
        // two processes collide on the audit queue, retention, forwarding and ingestion settings.
        static void EnsureRemotesAreNotCombinedWithLocalIngestion(Settings settings)
        {
            if (settings.RemoteInstances.Length == 0 || !settings.IngestAuditMessages || settings.AuditInstance)
            {
                return;
            }

            throw new Exception(
                $"This instance is configured to ingest audit messages into its own database and also lists {settings.RemoteInstances.Length} remote instance(s). "
                + "Set ServiceControl/AuditDataLocation to Remote if the audit data lives on a dedicated audit host, "
                + "set ServiceControl/IngestAuditMessages to false if only workers ingest, or remove the remotes.");
        }

        // Audit support has to be advertised by the persister, and the primary has to be told the data
        // is local rather than on a dedicated audit host.
        internal static bool HostsAuditData(Settings settings) =>
            SupportsAuditIngestion(settings) && settings.AuditDataLocation == AuditDataLocation.Local;

        internal static bool SupportsAuditIngestion(Settings settings) =>
            PersistenceManifestLibrary.Find(settings.PersistenceType)?.SupportsAuditIngestion ?? false;
    }
}
