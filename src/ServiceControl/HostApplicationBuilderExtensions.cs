namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.Hosting;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.Auth;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.Health;
    using global::ServiceControl.Infrastructure.Metrics;
    using global::ServiceControl.Infrastructure.WebApi;
    using global::ServiceControl.Notifications.Email;
    using global::ServiceControl.Operations.Metrics;
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
    using NServiceBus.Hosting;
    using NServiceBus.Transport;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using Particular.LicensingComponent;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Installers;
    using ServiceBus.Management.Infrastructure.Settings;

    static class HostApplicationBuilderExtensions
    {
        static readonly string InstanceVersion = FileVersionInfo.GetVersionInfo(typeof(HostApplicationBuilderExtensions).Assembly.Location).ProductVersion;

        public static void AddServiceControl(this IHostApplicationBuilder hostBuilder, Settings settings, EndpointConfiguration configuration, params ReadOnlySpan<ServiceControlComponent> components)
        {
            if (!settings.ErrorIngestionOnly)
            {
                ArgumentNullException.ThrowIfNull(configuration);
            }

            RecordStartup(settings, configuration);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserInteractive && Debugger.IsAttached)
            {
                EventSourceCreator.Create();
            }

            hostBuilder.Logging.ClearProviders();
            hostBuilder.Logging.ConfigureLogging(settings.LoggingSettings.LogLevel);

            var componentSetupContext = new ComponentInstallationContext();
            var serviceControlComponents = components is { Length: 0 } ? ServiceControlMainInstance.Components : components;
            foreach (ServiceControlComponent component in serviceControlComponents)
            {
                component.Setup(settings, componentSetupContext, hostBuilder);
            }

            var services = hostBuilder.Services;
            var transportSettings = settings.ToTransportSettings(componentSetupContext);
            var transportCustomization = TransportFactory.Create(transportSettings);
            transportCustomization.AddTransportForPrimary(services, transportSettings);

            services.Configure<HostOptions>(options => options.ShutdownTimeout = settings.ShutdownTimeout);
            services.AddSingleton<IDomainEvents, DomainEvents>();

            // Message-action audit trail. Registered here rather than in AddServiceControlAuthorization
            // because message handlers (archive/unarchive/edit/retry) depend on it, so it must exist in
            // every host that consumes the input queue — including --import-failed-errors, which never
            // wires up authorization.
            services.AddSingleton<IMessageActionAuditLog, MessageActionAuditLog>();

            services.AddSingleton(settings);
            services.AddEnvironmentDataProvider<ServiceControlErrorInstanceEnvironmentDataProvider>();

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

            services.AddPersistence(settings);
            services.AddMetrics(settings.PrintMetrics);
            hostBuilder.AddIngestionMetrics(settings);
            services.AddServiceControlHealthChecks();

            if (settings.ErrorIngestionOnly)
            {
                // Ingestion receives through its own transport infrastructure and forwards through
                // that same infrastructure's dispatcher, so the endpoint is not hosted at all.
                var machineName = NServiceBus.Support.RuntimeEnvironment.MachineName;
                services.AddSingleton(new HostInformation(
                    DeterministicGuid.MakeId(machineName, settings.InstanceName),
                    machineName));
                services.AddSingleton(provider => new CriticalError((context, _) =>
                {
                    provider.GetRequiredService<ILogger<CriticalError>>().LogCritical(context.Exception, "{CriticalError}", context.Error);
                    provider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                    return Task.CompletedTask;
                }));
            }
            else
            {
                services.AddLicenseCheck();

                NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, configuration);
                hostBuilder.Services.AddNServiceBusEndpoint(configuration);

                hostBuilder.AddEmailNotifications();
            }

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

            hostBuilder.AddServiceControlComponents(componentSetupContext, settings, transportCustomization, serviceControlComponents);
        }

        public static void AddServiceControlInstallers(this IHostApplicationBuilder hostApplicationBuilder, Settings settings)
        {
            var persistence = PersistenceFactory.Create(settings);
            persistence.AddInstaller(hostApplicationBuilder.Services);
        }

        public static void AddIngestionMetrics(this IHostApplicationBuilder hostBuilder, Settings settings)
        {
            hostBuilder.Services.AddSingleton<IngestionMetrics>();

            var otlpEndpoint = OtlpEndpoint.Read(hostBuilder.Configuration);

            if (otlpEndpoint is null)
            {
                return;
            }

            hostBuilder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                    serviceName: settings.InstanceName,
                    serviceVersion: InstanceVersion,
                    autoGenerateServiceInstanceId: true))
                .WithMetrics(metrics =>
                {
                    metrics.AddIngestionMetrics();
                    metrics.AddOtlpExporter();

                    if (Debugger.IsAttached)
                    {
                        metrics.AddConsoleExporter();
                    }
                });

            LoggerUtil.CreateStaticLogger(typeof(HostApplicationBuilderExtensions), settings.LoggingSettings.LogLevel)
                .LogInformation("OpenTelemetry metrics exporter enabled: {OtlpEndpoint}", otlpEndpoint);
        }

        static void RecordStartup(Settings settings, EndpointConfiguration endpointConfiguration)
        {
            var version = InstanceVersion;

            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:             {version}
Audit Retention Period (optional):  {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Error Ingestion Only:               {settings.ErrorIngestionOnly}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
ServiceControl Logging Level:       {settings.LoggingSettings.LogLevel}
Selected Transport Customization:   {settings.TransportType}
Integrated ServicePulse:            {(settings.EnableIntegratedServicePulse ? "Enabled" : "Disabled")}
-------------------------------------------------------------";

            var logger = LoggerUtil.CreateStaticLogger(typeof(HostApplicationBuilderExtensions), settings.LoggingSettings.LogLevel);
            logger.LogInformation(startupMessage);

            // There is no endpoint to hang diagnostics off in error ingestion only mode.
            endpointConfiguration?.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = settings,
            });
        }
    }
}