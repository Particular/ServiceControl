namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Auditing;
    using ServiceControl.Infrastructure.Health;
    using ServiceControl.Monitoring;

    /// <summary>
    /// Runs a host that does nothing but drain the audit queue into the shared database, so several
    /// processes can ingest against one database. Everything a deployment may only run once, the
    /// failed audit reimport command, API hosting, retention and licensing, stays with the primary
    /// instance, and this host never provisions queues, schema or body storage.
    /// </summary>
    class AuditIngestionOnlyCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            IngestionOnlyGuards.EnsureStorageSupportsAuditIngestion(settings);
            IngestionOnlyGuards.EnsureBodyStorageIsReadableByEveryHost("--audit-ingestion-only");

            var app = BuildHost(settings);

            await app.RunAsync(settings.RootUrl);
        }

        internal static WebApplication BuildHost(Settings settings, Action<WebApplicationBuilder> customize = null)
        {
            settings.AuditIngestionOnly = true;
            settings.IngestAuditMessages = true;
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;

            var hostBuilder = WebApplication.CreateBuilder();

            hostBuilder.AddServiceControl(settings, configuration: null, Components);

            customize?.Invoke(hostBuilder);

            var app = hostBuilder.Build();

            app.MapServiceControlHealthChecks();

            return app;
        }

        // EventLog and ExternalIntegrations are deliberately absent: audit ingestion raises no domain
        // events and no integration events. Hosting would claim the instance queue, and Licensing would
        // count throughput once per node.
        static ServiceControlComponent[] Components =>
        [
            new HeartbeatMonitoringComponent(),
            new CustomChecks.CustomChecksComponent(),
            new AuditComponent()
        ];
    }
}
