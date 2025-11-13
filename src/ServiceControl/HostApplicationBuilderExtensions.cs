namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.ExternalIntegrations;
    using global::ServiceControl.Hosting;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.Metrics;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Infrastructure.WebApi;
    using global::ServiceControl.Notifications.Email;
    using global::ServiceControl.Persistence;
    using global::ServiceControl.Transports;
    using Licensing;
    using Microsoft.AspNetCore.HttpLogging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Transport;
    using NuGet.Versioning;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    static class HostApplicationBuilderExtensions
    {
        public static void AddServiceControl(
            this IHostApplicationBuilder hostBuilder,
            Settings settings,
            EndpointConfiguration endpointConfiguration
        )
        {

            AddVersion(hostBuilder);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Debugger.IsAttached)
            {
                EventSourceCreator.Create();
            }

            hostBuilder.Logging.ClearProviders();
            hostBuilder.Logging.ConfigureLogging(settings.Logging.ToLoggingSettings().LogLevel);

            var services = hostBuilder.Services;
            var transportSettings = settings.ServiceControl.ToTransportSettings();
            var transportCustomization = TransportFactory.Create(transportSettings);
            transportCustomization.AddTransportForPrimary(services, transportSettings);

            services.Configure<HostOptions>(options => options.ShutdownTimeout = settings.ServiceControl.ShutdownTimeout);
            services.AddSingleton<IDomainEvents, DomainEvents>();

            services.AddSingleton<MessageStreamerHub>();
            services.AddSingleton(settings);

            services.AddHttpLogging(options =>
            {
                options.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
            });

            services.AddHttpContextAccessor();

            services.AddHttpForwarding();
            services.AddHttpClient();
            services.AddRemoteInstancesHttpClients(settings);

            // Core registers the message dispatcher to be resolved from the transport seam. The dispatcher
            // is only available though after the NServiceBus hosted service has started. Any hosted service
            // or component injected into a hosted service can only depend on this lazy instead of the dispatcher
            // directly and to make things more complex of course the order of registration still matters ;)
            services.AddSingleton(provider => new Lazy<IMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>));

            services.AddLicenseCheck();

            hostBuilder.AddPersistence();

            services.AddMetrics(settings.ServiceControl.PrintMetrics);

            NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, endpointConfiguration);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = settings,
            });
            hostBuilder.UseNServiceBus(endpointConfiguration);

            if (!settings.ServiceControl.DisableExternalIntegrationsPublishing)
            {
                hostBuilder.AddExternalIntegrationEvents();
            }

            hostBuilder.AddServicePulseSignalRNotifier();
            hostBuilder.AddEmailNotifications();
            hostBuilder.AddAsyncTimer();

            if (!settings.ServiceControl.DisableHealthChecks)
            {
                hostBuilder.AddInternalCustomChecks();
            }

            if (WindowsServiceHelpers.IsWindowsService())
            {
                // The if is added for clarity, internally AddWindowsService has a similar logic
                hostBuilder.AddWindowsServiceWithRequestTimeout();
            }

            hostBuilder.AddServiceControlComponents(settings, transportCustomization, ServiceControlMainInstance.Components);
        }

        static void AddVersion(IHostApplicationBuilder hostBuilder) => hostBuilder.Services.AddSingleton(NuGetVersion.Parse(FileVersionInfo.GetVersionInfo(typeof(HostApplicationBuilderExtensions).Assembly.Location).ProductVersion));

        public static void AddServiceControlInstallers(this IHostApplicationBuilder hostApplicationBuilder)
        {
            var persistence = PersistenceFactory.Create(hostApplicationBuilder.Configuration);
            persistence.AddInstaller(hostApplicationBuilder.Services);
        }
    }

    public class RecordStartup(
        ILogger<RecordStartup> logger,
        IOptions<PrimaryOptions> primaryOptions,
        IOptions<LoggingOptions> loggingOptions,
        NuGetVersion version
    ) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var primary = primaryOptions.Value;
            var logging = loggingOptions.Value;

            var startupMessage = $"""
                                  -------------------------------------------------------------
                                  ServiceControl Version:             {version}
                                  Audit Retention Period (optional):  {primary.AuditRetentionPeriod}
                                  Error Retention Period:             {primary.ErrorRetentionPeriod}
                                  Ingest Error Messages:              {primary.IngestErrorMessages}
                                  Forwarding Error Messages:          {primary.ForwardErrorMessages}
                                  ServiceControl Logging Level:       {logging.LogLevel}
                                  Selected Transport Customization:   {primary.TransportType}
                                  ------------------------------------------------------------
                                  """;

            logger.LogInformation(startupMessage);
            return Task.CompletedTask;
        }
    }
}