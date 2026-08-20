namespace ServiceControl.Auditing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.LicensingComponent.AuditThroughput;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing.Metrics;
    using ServiceControl.Connection;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Health;
    using ServiceControl.Persistence;
    using ServiceControl.Transports;

    // Registers nothing unless the configured persister advertises audit support in its manifest, so
    // hosts on a persister that cannot store audit data behave exactly as they did before.
    class AuditComponent : ServiceControlComponent
    {
        public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
        {
            if (!SupportsAuditIngestion(settings))
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
            if (!SupportsAuditIngestion(settings))
            {
                return;
            }

            WarnAboutSettingCollisions(settings);

            var services = hostBuilder.Services;

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

            if (!settings.IngestionOnly)
            {
                // Registered before the licensing component's own fallback, which uses TryAdd.
                services.AddSingleton<ILocalAuditSource, PrimaryLocalAuditSource>();
                services.AddPlatformConnectionProvider<AuditPlatformConnectionDetailsProvider>();
            }
        }

        // ServiceControl and ServiceControl.Audit settings can both be set by bare environment variable
        // name, and ServiceBus/AuditQueue is literally the same key for both processes, so a combined
        // primary and a standalone audit instance sharing one environment file collide. That
        // combination is unsupported, and this is the shape most likely to hit it.
        static void WarnAboutSettingCollisions(Settings settings)
        {
            if (settings.RemoteInstances.Length == 0)
            {
                return;
            }

            LoggerUtil.CreateStaticLogger(typeof(AuditComponent), settings.LoggingSettings.LogLevel)
                .LogWarning("This instance ingests audit messages itself and also has {RemoteInstanceCount} audit remote(s) configured. "
                    + "Running both is not supported: the two processes read the same setting names, so a shared environment file makes them "
                    + "collide on the audit queue, retention, forwarding and ingestion settings.", settings.RemoteInstances.Length);
        }

        internal static bool SupportsAuditIngestion(Settings settings) =>
            PersistenceManifestLibrary.Find(settings.PersistenceType)?.SupportsAuditIngestion ?? false;
    }
}
