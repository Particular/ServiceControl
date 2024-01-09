namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.ExternalIntegrations;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.Metrics;
    using global::ServiceControl.Infrastructure.OWIN;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Infrastructure.WebApi;
    using global::ServiceControl.Notifications.Email;
    using global::ServiceControl.Persistence;
    using global::ServiceControl.Transports;
    using Licensing;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    class Bootstrapper
    {
        // Windows Service
        public Bootstrapper(Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            ApiAssemblies = [Assembly.GetExecutingAssembly()];

            CreateHost();
        }

        public WebApplicationBuilder HostBuilder { get; private set; }

        public List<Assembly> ApiAssemblies { get; }

        void CreateHost()
        {
            RecordStartup(loggingSettings, configuration);

            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                configuration.License(settings.LicenseFileText);
            }

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                EventSourceCreator.Create();
            }

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            transportCustomization = settings.LoadTransportCustomization();
            transportSettings = MapSettings(settings);

            HostBuilder = WebApplication.CreateBuilder();
            var logging = HostBuilder.Logging;
            logging.ClearProviders();
            //HINT: configuration used by NLog comes from LoggingConfigurator.cs
            logging.AddNLog();
            logging.SetMinimumLevel(loggingSettings.ToHostLogLevel());

            var services = HostBuilder.Services;
            services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<IDomainEvents, DomainEvents>();
            services.AddSingleton(transportSettings);
            services.AddSingleton(transportCustomization);

            services.AddSingleton<MessageStreamerHub>();
            services.AddSingleton(loggingSettings);
            services.AddSingleton(settings);

            services.AddHttpContextAccessor();
            services.AddRemoteInstancesHttpClients(settings);

            // Core registers the message dispatcher to be resolved from the transport seam. The dispatcher
            // is only available though after the NServiceBus hosted service has started. Any hosted service
            // or component injected into a hosted service can only depend on this lazy instead of the dispatcher
            // directly and to make things more complex of course the order of registration still matters ;)
            services.AddSingleton(provider => new Lazy<IMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>));

            HostBuilder.UseLicenseCheck();
            HostBuilder.SetupPersistence(settings);
            HostBuilder.UseMetrics(settings.PrintMetrics);
            HostBuilder.Host.UseNServiceBus(context =>
            {
                NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, configuration);
                return configuration;
            });

            if (!settings.DisableExternalIntegrationsPublishing)
            {
                HostBuilder.UseExternalIntegrationEvents();
            }

            HostBuilder.UseWebApi(ApiAssemblies, settings.RootUrl, settings.ExposeApi);
            HostBuilder.UseServicePulseSignalRNotifier();
            HostBuilder.UseEmailNotifications();
            HostBuilder.UseAsyncTimer();

            if (!settings.DisableHealthChecks)
            {
                HostBuilder.UseInternalCustomChecks();
            }

            if (settings.RunAsWindowsService)
            {

                HostBuilder.Services.AddWindowsService();
            }

            HostBuilder.UseServiceControlComponents(settings, ServiceControlMainInstance.Components);
        }

        public async Task Boot()
        {
            using var app = HostBuilder.Build();

            app.UseResponseCompression();
            app.UseMiddleware<BodyUrlRouteFix>();
            app.UseMiddleware<LogApiCalls>();
            app.MapHub<MessageStreamerHub>("/api/messagestream");
            app.UseCors();
            app.UseRouting();
            app.MapControllers();

            // Initialized IDocumentStore, this is needed as many hosted services have (indirect) dependencies on it.
            // TODO: isn't it better to make the Initialize method idempotent and move the initialization to the container dependency factory?
            await app.Services.GetRequiredService<IPersistenceLifecycle>().Initialize();
            await app.RunAsync(settings.RootUrl);
        }

        TransportSettings MapSettings(Settings settings)
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

        void RecordStartup(LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Bootstrapper).Assembly.Location).ProductVersion;

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

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = settings,
                LoggingSettings = loggingSettings
            });
        }

        EndpointConfiguration configuration;
        LoggingSettings loggingSettings;
        Settings settings;
        TransportSettings transportSettings;
        ITransportCustomization transportCustomization;
    }
}