namespace ServiceControl.Auditing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing.Metrics;
    using ServiceControl.CustomChecks;
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

            var services = hostBuilder.Services;

            services.AddSingleton<AuditIngestionMetrics>();
            services.AddSingleton<AuditIngestor>();
            services.AddSingleton<ImportFailedAudits>();
            services.AddSingleton<AuditIngestionCustomCheck.State>();

            services.AddCustomCheck<AuditIngestionCustomCheck>();
            services.AddCustomCheck<FailedAuditImportCustomCheck>();

            if (settings.IngestAuditMessages)
            {
                services.AddHostedService<AuditIngestion>();
            }

            hostBuilder.AddAuditIngestionOpenTelemetry(settings);
        }

        internal static bool SupportsAuditIngestion(Settings settings) =>
            PersistenceManifestLibrary.Find(settings.PersistenceType)?.SupportsAuditIngestion ?? false;
    }
}
