namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl;
    using ServiceControl.Hosting.Auth;
    using ServiceControl.Hosting.Https;
    using ServiceControl.Migration;
    using ServicePulse;

    class RunCommand : AbstractCommand
    {
        public override Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default) =>
            Run(settings, customize: null, cancellationToken);

        /// <summary>
        /// Builds and runs the full instance. When the migration is turned on, the required copy is added as the
        /// first thing the host starts, so the instance opens only once the copy has finished.
        /// </summary>
        internal static async Task Run(Settings settings, Action<WebApplicationBuilder> customize, CancellationToken cancellationToken = default)
        {
            var endpointConfiguration = new EndpointConfiguration(settings.InstanceName);
            var assemblyScanner = endpointConfiguration.AssemblyScanner();
            assemblyScanner.Disable = true;

            settings.RunCleanupBundle = true;

            var hostBuilder = WebApplication.CreateBuilder();

            hostBuilder.AddServiceControlAuthentication(settings.OpenIdConnectSettings);
            hostBuilder.AddServiceControlAuthorization(settings.OpenIdConnectSettings);
            hostBuilder.AddServiceControlHttps(settings.HttpsSettings);
            hostBuilder.AddServiceControl(settings, endpointConfiguration);
            hostBuilder.AddServiceControlApi(settings.CorsSettings);

            customize?.Invoke(hostBuilder);

            if (settings.MigrationEnabled)
            {
                hostBuilder.Services.AddHostedService(provider => new RequiredCopyBeforeTheHostOpens(provider, settings));
            }

            // A start that refuses would otherwise leave everything the host built undisposed.
            await using var app = hostBuilder.Build();

            app.UseServiceControl(settings.ForwardedHeadersSettings, settings.HttpsSettings);
            if (settings.EnableIntegratedServicePulse)
            {
                app.UseServicePulse(settings.ServicePulseSettings);
            }
            app.UseServiceControlAuthentication(settings.OpenIdConnectSettings.Enabled);

            // WebApplication's RunAsync(url) takes no cancellation token, so set the url here and call IHost's RunAsync below.
            app.Urls.Clear();
            app.Urls.Add(settings.RootUrl);

            // Starting the host is what marks the target as opened, through the EF Core persistence's
            // RecordHostOpenedOnTarget hosted service, so nothing here does it.
            try
            {
                await app.RunAsync(cancellationToken);
            }
            // Stopping the service during the required copy cancels it inside host start, and that is a stop
            // rather than a failure: every committed batch is durable and the next start resumes from the cursor.
#pragma warning disable PS0020 // The host cancels on its own lifetime token, not the caller's, so that is the one to filter on
            catch (OperationCanceledException) when (app.Lifetime.ApplicationStopping.IsCancellationRequested)
#pragma warning restore PS0020
            {
            }
        }
    }
}
