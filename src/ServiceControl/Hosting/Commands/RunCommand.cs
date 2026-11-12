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
#if DEBUG
    using Microsoft.Extensions.Logging;
    using ServiceControl.Infrastructure;
    using ServiceControl.Migration.Checks;
#endif
    using ServicePulse;

    class RunCommand : AbstractCommand
    {
#if DEBUG
        internal const string AllowUnreleasedMigrationVariable = "SERVICECONTROL_MIGRATION_ALLOW_UNRELEASED";

        /// <summary>
        /// When <paramref name="variableValue"/> is "true" in any letter case, registers <see cref="AllowUnreleasedMigration"/>
        /// so a Debug build can copy past the release check for a developer's smoke test, and logs a warning so the bypass shows in the log.
        /// </summary>
        internal static void AllowUnreleasedMigrationWhenAsked(IServiceCollection services, string variableValue)
        {
            if (string.Equals(variableValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<AllowUnreleasedMigration>();
                LoggerUtil.CreateStaticLogger<RunCommand>().LogWarning("{Variable} is true, so this Debug build skips the check that the build carries the whole migration and will copy for real", AllowUnreleasedMigrationVariable);
            }
        }
#endif

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

#if DEBUG
            AllowUnreleasedMigrationWhenAsked(hostBuilder.Services, Environment.GetEnvironmentVariable(AllowUnreleasedMigrationVariable));
#endif

            if (settings.MigrationEnabled)
            {
                hostBuilder.Services.AddHostedService(provider => new RequiredCopyBeforeTheHostOpens(provider, settings));
                hostBuilder.Services.AddHostedService(provider => new RecordHostOpenedOnTarget(provider));
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

            // Read before RunAsync, which disposes the host before the filter below runs, so reading it there would
            // throw and the catch would never fire.
            var lifetime = app.Lifetime;

            // Starting the host is what marks the target as opened, through the RecordHostOpenedOnTarget hosted
            // service registered above, so nothing here does it.
            try
            {
                await app.RunAsync(cancellationToken);
            }
            // Stopping the service during the required copy cancels it inside host start, and that is a stop
            // rather than a failure: every committed batch is durable and the next start resumes from the cursor.
#pragma warning disable PS0020 // The host cancels on its own lifetime token, not the caller's, so that is the one to filter on
            catch (OperationCanceledException) when (settings.MigrationEnabled && lifetime.ApplicationStopping.IsCancellationRequested)
#pragma warning restore PS0020
            {
            }
        }
    }
}
