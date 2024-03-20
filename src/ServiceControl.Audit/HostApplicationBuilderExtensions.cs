namespace ServiceControl.Audit;

using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Auditing;
using Infrastructure;
using Infrastructure.Metrics;
using Infrastructure.Settings;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitoring;
using NLog.Extensions.Logging;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Logging;
using NServiceBus.Transport;
using Persistence;
using Transports;

static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlAudit(this IHostApplicationBuilder builder,
        Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError,
        Settings settings,
        EndpointConfiguration configuration,
        LoggingSettings loggingSettings)
    {
        var persistenceConfiguration =
            PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceType);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

        RecordStartup(settings, loggingSettings, configuration, persistenceConfiguration);

        if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
        {
            configuration.License(settings.LicenseFileText);
        }

        var transportSettings = MapSettings(settings);
        var transportCustomization = settings.LoadTransportCustomization();

        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();
        builder.Logging.SetMinimumLevel(loggingSettings.ToHostLogLevel());

        var services = builder.Services;

        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        services.AddSingleton(transportSettings);
        services.AddSingleton(transportCustomization);

        services.AddSingleton(loggingSettings);
        services.AddSingleton(settings);
        services.AddSingleton<EndpointInstanceMonitoring>();
        services.AddSingleton<AuditIngestor>();
        services.AddSingleton<ImportFailedAudits>();
        services.AddSingleton<AuditIngestionCustomCheck.State>(); // required by the ingestion custom check which is auto-loaded

        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
        });

        // Core registers the message dispatcher to be resolved from the transport seam. The dispatcher
        // is only available though after the NServiceBus hosted service has started. Any hosted service
        // or component injected into a hosted service can only depend on this lazy instead of the dispatcher
        // directly and to make things more complex of course the order of registration still matters ;)
        services.AddSingleton(provider => new Lazy<IMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>));

        services.AddMetrics(settings.PrintMetrics);

        services.AddPersistence(persistenceSettings, persistenceConfiguration);

        NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, onCriticalError, configuration);
        builder.UseNServiceBus(configuration);

        // Configure after the NServiceBus hosted service to ensure NServiceBus is already started
        if (settings.IngestAuditMessages)
        {
            services.AddHostedService<AuditIngestion>();
        }

        builder.Services.AddWindowsService();
    }

    static TransportSettings MapSettings(Settings settings)
    {
        var transportSettings = new TransportSettings
        {
            EndpointName = settings.ServiceName,
            ConnectionString = settings.TransportConnectionString,
            MaxConcurrency = settings.MaximumConcurrencyLevel
        };
        return transportSettings;
    }

    static void RecordStartup(Settings settings, LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration, IPersistenceConfiguration persistenceConfiguration)
    {
        var version = FileVersionInfo.GetVersionInfo(typeof(HostApplicationBuilderExtensions).Assembly.Location).ProductVersion;

        var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Audit Version:       {version}
Audit Retention Period:             {settings.AuditRetentionPeriod}
Forwarding Audit Messages:          {settings.ForwardAuditMessages}
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
Transport Customization:            {settings.TransportType},
Persistence Customization:          {settings.PersistenceType},
Persistence:                        {persistenceConfiguration.Name}
-------------------------------------------------------------";

        var logger = LogManager.GetLogger(typeof(HostApplicationBuilderExtensions));
        logger.Info(startupMessage);
        endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
        {
            Settings = settings,
            LoggingSettings = loggingSettings
        });
    }
}