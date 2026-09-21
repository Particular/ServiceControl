namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.EventLog;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure.Health;
    using ServiceControl.Migration;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.DataMigration;
    using ServiceControl.Recoverability;

    /// <summary>
    /// Runs a host that does nothing but drain the error queue into the shared database, so several
    /// processes can ingest against one database. Everything a deployment may only run once, the
    /// retry pipeline, the retention sweep, integration event dispatch and heartbeat monitoring,
    /// stays with the primary instance.
    /// </summary>
    class ErrorIngestionOnlyCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            EnsureStorageCanScaleOut(settings);

            var app = BuildHost(settings);

            await app.RunAsync();
        }

        internal static WebApplication BuildHost(Settings settings, Action<WebApplicationBuilder> customize = null)
        {
            settings.ErrorIngestionOnly = true;
            settings.IngestErrorMessages = true;
            settings.RunRetryProcessor = false;

            var hostBuilder = WebApplication.CreateBuilder();

            hostBuilder.AddServiceControl(settings, configuration: null, Components);

            hostBuilder.Services.AddHostedService(provider =>
                new FinishedCopyBeforeAnIngestionNodeOpens(provider.GetRequiredService<IMigrationCheckpointStore>(), settings));

            customize?.Invoke(hostBuilder);

            var app = hostBuilder.Build();

            app.MapServiceControlHealthChecks();

            // Set here rather than passed to RunAsync, so a caller that starts the host itself gets the
            // configured address instead of Kestrel's default port.
            app.Urls.Clear();
            app.Urls.Add(settings.RootUrl);

            return app;
        }

        static void EnsureStorageCanScaleOut(Settings settings)
        {
            var manifest = PersistenceManifestLibrary.Find(settings.PersistenceType);

            if (manifest == null || !PersistenceFactory.SqlPersistenceNames.Contains(manifest.Name, StringComparer.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"--error-ingestion-only requires SQL Server or PostgreSQL storage, but this instance is configured to use '{settings.PersistenceType}'. Scaling out error ingestion is not supported for this storage type.");
            }
        }

        static ServiceControlComponent[] Components =>
        [
            new EventLogComponent(),
            new ExternalIntegrationsComponent(),
            new RecoverabilityComponent(),
            new HeartbeatMonitoringComponent(),
            new CustomChecks.CustomChecksComponent()
        ];
    }
}
