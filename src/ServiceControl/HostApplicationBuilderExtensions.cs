namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
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
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    static class HostApplicationBuilderExtensions
    {
        public static void AddServiceControl(this IHostApplicationBuilder hostBuilder, Settings settings, EndpointConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            RecordStartup(settings, configuration);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Debugger.IsAttached)
            {
                EventSourceCreator.Create();
            }

            hostBuilder.Logging.ClearProviders();
            hostBuilder.Logging.BuildLogger(settings.LoggingSettings.LogLevel);

            var services = hostBuilder.Services;
            var transportSettings = settings.ToTransportSettings();
            var transportCustomization = TransportFactory.Create(transportSettings);
            transportCustomization.AddTransportForPrimary(services, transportSettings);

            services.Configure<HostOptions>(options => options.ShutdownTimeout = settings.ShutdownTimeout);
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
            services.AddPersistence(settings);
            services.AddMetrics(settings.PrintMetrics);

            NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, configuration);
            hostBuilder.UseNServiceBus(configuration);

            if (!settings.DisableExternalIntegrationsPublishing)
            {
                hostBuilder.AddExternalIntegrationEvents();
            }

            hostBuilder.AddServicePulseSignalRNotifier();
            hostBuilder.AddEmailNotifications();
            hostBuilder.AddAsyncTimer();

            if (!settings.DisableHealthChecks)
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

        public static void AddServiceControlInstallers(this IHostApplicationBuilder hostApplicationBuilder, Settings settings)
        {
            var persistence = PersistenceFactory.Create(settings);
            persistence.AddInstaller(hostApplicationBuilder.Services);
        }

        static void RecordStartup(Settings settings, EndpointConfiguration endpointConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(HostApplicationBuilderExtensions).Assembly.Location).ProductVersion;

            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:             {version}
Audit Retention Period (optional):  {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
ServiceControl Logging Level:       {settings.LoggingSettings.LogLevel}
Selected Transport Customization:   {settings.TransportType}
-------------------------------------------------------------";

            var logger = LoggerUtil.CreateStaticLogger(typeof(HostApplicationBuilderExtensions), settings.LoggingSettings.LogLevel);
            logger.LogInformation(startupMessage);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = settings,
            });
        }
    }
}