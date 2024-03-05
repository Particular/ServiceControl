namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.ExternalIntegrations;
    using global::ServiceControl.Hosting;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.Metrics;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Infrastructure.WebApi;
    using global::ServiceControl.Notifications.Email;
    using global::ServiceControl.Persistence;
    using global::ServiceControl.Transports;
    using Licensing;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpLogging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Hosting.WindowsServices;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;
    using Particular.ThroughputCollector;

    static class WebApplicationBuilderExtension
    {
        public static void AddServiceControl(this WebApplicationBuilder hostBuilder, Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            RecordStartup(settings, loggingSettings, configuration);

            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                configuration.License(settings.LicenseFileText);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Debugger.IsAttached)
            {
                EventSourceCreator.Create();
            }

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            var transportCustomization = settings.LoadTransportCustomization();
            var transportSettings = MapSettings(settings);

            var logging = hostBuilder.Logging;
            logging.ClearProviders();
            //HINT: configuration used by NLog comes from LoggingConfigurator.cs
            logging.AddNLog();
            logging.SetMinimumLevel(loggingSettings.ToHostLogLevel());

            var services = hostBuilder.Services;
            services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<IDomainEvents, DomainEvents>();
            services.AddSingleton(transportSettings);
            services.AddSingleton(transportCustomization);

            services.AddSingleton<MessageStreamerHub>();
            services.AddSingleton(loggingSettings);
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

            hostBuilder.AddLicenseCheck();
            services.AddPersistence(settings);
            services.AddMetrics(settings.PrintMetrics);
            hostBuilder.Host.UseNServiceBus(_ =>
            {
                NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, configuration);
                return configuration;
            });

            if (!settings.DisableExternalIntegrationsPublishing)
            {
                hostBuilder.AddExternalIntegrationEvents();
            }

            hostBuilder.AddWebApi([Assembly.GetExecutingAssembly()], settings.RootUrl);

            hostBuilder.AddServicePulseSignalRNotifier();
            hostBuilder.AddEmailNotifications();
            hostBuilder.AddAsyncTimer();

            if (!settings.DisableHealthChecks)
            {
                hostBuilder.AddInternalCustomChecks();
            }

            hostBuilder.Services.AddWindowsService();

            if (WindowsServiceHelpers.IsWindowsService())
            {
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingWindowsServiceLifetime>();
            }
            else
            {
                hostBuilder.Services.AddSingleton<IHostLifetime, PersisterInitializingConsoleLifetime>();
            }

            hostBuilder.AddServiceControlComponents(settings, ServiceControlMainInstance.Components);
        }

        static TransportSettings MapSettings(Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel,
                RunCustomChecks = true
            };

            return transportSettings;
        }

        static void RecordStartup(Settings settings, LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(WebApplicationBuilderExtension).Assembly.Location).ProductVersion;

            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:             {version}
Audit Retention Period (optional):  {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
Selected Transport Customization:   {settings.TransportType}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(WebApplicationBuilderExtension));
            logger.Info(startupMessage);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = settings,
                LoggingSettings = loggingSettings
            });
        }
    }
}