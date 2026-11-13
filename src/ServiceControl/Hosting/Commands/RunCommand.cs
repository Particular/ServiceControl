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
        /// Builds and runs the full instance after checking that any offline migration has finished.
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

            hostBuilder.Services.AddHostedService(provider => new MigrationBeforeTheHostOpens(provider));
            hostBuilder.Services.AddHostedService(provider => new RecordHostOpenedOnTarget(provider));
            hostBuilder.Services.AddHostedService<OptionalCategoryCopier>();

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

            await app.RunAsync(cancellationToken);
        }
    }
}
