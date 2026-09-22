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
    using ServiceControl.Hosting.Auth;
    using ServiceControl.Hosting.Https;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Monitoring;

    /// <summary>
    /// The owner of a dedicated audit database: ingests the audit queue, serves the API the primary's
    /// scatter gather calls, sweeps audit retention, and reports its custom checks and the endpoints
    /// it detects to the primary named by ServiceControlQueueAddress. No error side at all, and no
    /// primary NServiceBus endpoint, only a send only one for the reporting.
    /// </summary>
    class AuditInstanceCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            AuditInstanceGuards.EnsureCanRun(settings);

            var app = BuildHost(settings, hostBuilder =>
            {
                hostBuilder.AddServiceControlAuthentication(settings.OpenIdConnectSettings);
                hostBuilder.AddServiceControlAuthorization(settings.OpenIdConnectSettings);
                hostBuilder.AddServiceControlHttps(settings.HttpsSettings);
            });

            app.UseServiceControl(settings.ForwardedHeadersSettings, settings.HttpsSettings);
            app.UseServiceControlAuthentication(settings.OpenIdConnectSettings.Enabled);

            await app.RunAsync(settings.RootUrl);
        }

        internal static WebApplication BuildHost(Settings settings, Action<WebApplicationBuilder> customize = null)
        {
            ApplyMode(settings);

            var hostBuilder = WebApplication.CreateBuilder();

            customize?.Invoke(hostBuilder);

            hostBuilder.AddServiceControl(settings, configuration: null, Components);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            return hostBuilder.Build();
        }

        // Shared with setup, so that provisioning and running agree on what this host is.
        internal static void ApplyMode(Settings settings)
        {
            settings.AuditInstance = true;
            settings.AuditDataLocation = AuditDataLocation.Local;
            settings.IngestAuditMessages = true;
            settings.IngestErrorMessages = false;
            settings.RunRetryProcessor = false;
            settings.DisableExternalIntegrationsPublishing = true;
        }

        // Heartbeat monitoring warms the endpoint monitor the audit enricher asks, custom checks
        // report this host's ingestion health, and the audit component is the reason the host exists.
        internal static ServiceControlComponent[] Components =>
        [
            new HeartbeatMonitoringComponent(),
            new CustomChecks.CustomChecksComponent(),
            new AuditComponent()
        ];
    }
}
