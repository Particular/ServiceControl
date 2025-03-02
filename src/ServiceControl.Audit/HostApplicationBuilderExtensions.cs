namespace ServiceControl.Audit;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Auditing;
using Hosting;
using Infrastructure;
using Infrastructure.Settings;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Monitoring;
using NLog.Extensions.Logging;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Logging;
using NServiceBus.Transport;
using Persistence;
using Transports;
using ServiceControl.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlAudit(this IHostApplicationBuilder builder,
        Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError,
        Settings settings,
        EndpointConfiguration configuration)
    {
        var version = FileVersionInfo.GetVersionInfo(typeof(HostApplicationBuilderExtensions).Assembly.Location).ProductVersion;
        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

        RecordStartup(version, settings, configuration, persistenceConfiguration);

        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();
        builder.Logging.SetMinimumLevel(settings.LoggingSettings.ToHostLogLevel());

        var services = builder.Services;
        var transportSettings = settings.ToTransportSettings();
        var transportCustomization = TransportFactory.Create(transportSettings);
        transportCustomization.AddTransportForAudit(services, transportSettings);

        services.Configure<HostOptions>(options => options.ShutdownTimeout = settings.ShutdownTimeout);

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

        services.AddPersistence(persistenceSettings, persistenceConfiguration);

        NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, onCriticalError, configuration);
        builder.UseNServiceBus(configuration);

        services.AddSingleton<AuditIngestionMetrics>();

        if (!string.IsNullOrEmpty(settings.OtlpEndpointUrl))
        {
            if (!Uri.TryCreate(settings.OtlpEndpointUrl, UriKind.Absolute, out var otelMetricsUri))
            {
                throw new UriFormatException($"Invalid OtlpEndpointUrl: {settings.OtlpEndpointUrl}");
            }

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(b => b.AddService(
                    serviceName: settings.InstanceName,
                    serviceVersion: version,
                    autoGenerateServiceInstanceId: true))
                .WithMetrics(b =>
                {
                    b.AddAuditIngestionMeters();
                    b.AddOtlpExporter(e =>
                    {
                        e.Endpoint = otelMetricsUri;
                    });
                });

            var logger = LogManager.GetLogger(typeof(HostApplicationBuilderExtensions));
            logger.InfoFormat("OpenTelemetry metrics exporter enabled: {0}", settings.OtlpEndpointUrl);
        }

        // Configure after the NServiceBus hosted service to ensure NServiceBus is already started
        if (settings.IngestAuditMessages)
        {
            services.AddHostedService<AuditIngestion>();
        }

        if (WindowsServiceHelpers.IsWindowsService())
        {
            // The if is added for clarity, internally AddWindowsService has a similar logic
            builder.AddWindowsServiceWithRequestTimeout();
        }
    }

    public static void AddServiceControlAuditInstallers(this IHostApplicationBuilder builder, Settings settings)
    {
        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);
        builder.Services.AddInstaller(persistenceSettings, persistenceConfiguration);
    }

    static void RecordStartup(string version, Settings settings, EndpointConfiguration endpointConfiguration, IPersistenceConfiguration persistenceConfiguration)
    {
        var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Audit Version:       {version}
Audit Retention Period:             {settings.AuditRetentionPeriod}
Forwarding Audit Messages:          {settings.ForwardAuditMessages}
ServiceControl Logging Level:       {settings.LoggingSettings.LogLevel}
Transport Customization:            {settings.TransportType},
Persistence Customization:          {settings.PersistenceType},
Persistence:                        {persistenceConfiguration.Name}
-------------------------------------------------------------";

        var logger = LogManager.GetLogger(typeof(HostApplicationBuilderExtensions));
        logger.Info(startupMessage);
        endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new { Settings = settings });
    }
}