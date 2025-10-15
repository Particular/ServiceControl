namespace ServiceControl.Monitoring;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Hosting;
using Infrastructure;
using Infrastructure.BackgroundTasks;
using Infrastructure.Extensions;
using Licensing;
using Messaging;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;
using NServiceBus.Transport;
using Particular.LicensingComponent.Shared;
using QueueLength;
using ServiceControl.Infrastructure;
using Timings;
using Transports;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlMonitoring(
        this IHostApplicationBuilder hostBuilder,
        Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError,
        Settings settings,
        EndpointConfiguration endpointConfiguration
    )
    {
        var section = hostBuilder.Configuration.GetSection(Settings.SectionName);
        //hostBuilder.Services.ConfigureOptions<MonitoringOptionsSetup>();

        hostBuilder.Services.AddLogging();
        hostBuilder.Logging.ConfigureLogging(settings.LoggingSettings.LogLevel);

        var services = hostBuilder.Services;

        var transportSettings = settings.ToTransportSettings();
        var transportCustomization = TransportFactory.Create(transportSettings);
        transportCustomization.AddTransportForMonitoring(services, transportSettings);

        services.Configure<HostOptions>(options => options.ShutdownTimeout = settings.ShutdownTimeout);

        if (WindowsServiceHelpers.IsWindowsService())
        {
            // The if is added for clarity, internally AddWindowsService has a similar logic
            hostBuilder.AddWindowsServiceWithRequestTimeout();
        }

        services.AddSingleton(settings);
        services.AddSingleton<EndpointRegistry>();
        services.AddSingleton<MessageTypeRegistry>();
        services.AddSingleton<EndpointInstanceActivityTracker>();
        services.AddSingleton<LegacyQueueLengthReportHandler.LegacyQueueLengthEndpoints>();
        services.AddSingleton<PlatformEndpointHelper>();
        services.AddSingleton<ServiceControlSettings>();

        services.RegisterAsSelfAndImplementedInterfaces<RetriesStore>();
        services.RegisterAsSelfAndImplementedInterfaces<CriticalTimeStore>();
        services.RegisterAsSelfAndImplementedInterfaces<ProcessingTimeStore>();
        services.RegisterAsSelfAndImplementedInterfaces<QueueLengthStore>();
        services.AddSingleton<Action<QueueLengthEntry[], EndpointToQueueMapping>>(provider => (es, q) =>
            provider.GetRequiredService<QueueLengthStore>().Store(es.Select(e => ToEntry(e)).ToArray(), ToQueueId(q)));

        services.AddHttpLogging(options =>
        {
            options.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.RequestMethod | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
        });

        // Core registers the message dispatcher to be resolved from the transport seam. The dispatcher
        // is only available though after the NServiceBus hosted service has started. Any hosted service
        // or component injected into a hosted service can only depend on this lazy instead of the dispatcher
        // directly and to make things more complex of course the order of registration still matters ;)
        services.AddSingleton(provider => new Lazy<IMessageDispatcher>(provider.GetRequiredService<IMessageDispatcher>));

        services.AddLicenseCheck();

        ConfigureEndpoint(endpointConfiguration, onCriticalError, transportCustomization, transportSettings, settings, services);
        hostBuilder.UseNServiceBus(endpointConfiguration);

        hostBuilder.AddAsyncTimer();
    }

    static void ConfigureEndpoint(EndpointConfiguration config, Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError, ITransportCustomization transportCustomization, TransportSettings transportSettings, Settings settings, IServiceCollection services)
    {
        transportCustomization.CustomizeMonitoringEndpoint(config, transportSettings);

        var serviceControlThroughputDataQueue = settings.ServiceControlThroughputDataQueue;
        if (!string.IsNullOrWhiteSpace(serviceControlThroughputDataQueue))
        {
            services.AddHostedService<ReportThroughputHostedService>();
        }

        services.AddHostedService<RemoveExpiredEndpointInstances>();

        config.DefineCriticalErrorAction(onCriticalError);

        config.GetSettings().Set(settings);
        config.SetDiagnosticsPath(settings.LoggingSettings.LogPath);
        if (!transportSettings.MaxConcurrency.HasValue)
        {
            throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
        }
        config.LimitMessageProcessingConcurrencyTo(transportSettings.MaxConcurrency.Value);

        config.UseSerialization<SystemJsonSerializer>();
        config.UsePersistence<NonDurablePersistence>();

        var recoverability = config.Recoverability();
        recoverability.Immediate(c => c.NumberOfRetries(3));
        recoverability.Delayed(c => c.NumberOfRetries(0));

        config.SendFailedMessagesTo(transportCustomization.ToTransportQualifiedQueueName(settings.ErrorQueue));

        config.DisableFeature<AutoSubscribe>();

        config.AddDeserializer<TaggedLongValueWriterOccurrenceSerializerDefinition>();
        config.Pipeline.Register(typeof(MessagePoolReleasingBehavior), "Releases pooled message.");

        if (AppEnvironment.RunningInContainer)
        {
            // Do not write diagnostics file
            config.CustomDiagnosticsWriter((_, _) => Task.CompletedTask);
        }
    }

    static EndpointInputQueue ToQueueId(EndpointToQueueMapping endpointInputQueueDto) =>
        new(endpointInputQueueDto.EndpointName, endpointInputQueueDto.InputQueue);

    static RawMessage.Entry ToEntry(QueueLengthEntry entryDto) => new() { DateTicks = entryDto.DateTicks, Value = entryDto.Value };
}