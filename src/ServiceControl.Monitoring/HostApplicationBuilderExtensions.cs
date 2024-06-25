namespace ServiceControl.Monitoring;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Infrastructure;
using Infrastructure.BackgroundTasks;
using Infrastructure.Extensions;
using Licensing;
using Messaging;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;
using NServiceBus.Metrics;
using NServiceBus.Transport;
using QueueLength;
using Timings;
using Transports;

public static class HostApplicationBuilderExtensions
{
    public static void AddServiceControlMonitoring(this IHostApplicationBuilder hostBuilder,
        Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError, Settings settings,
        EndpointConfiguration endpointConfiguration)
    {
        var transportCustomization = settings.LoadTransportCustomization();
        var buildQueueLengthProvider = QueueLengthProviderBuilder(settings.ConnectionString, transportCustomization);

        hostBuilder.Services.AddWindowsService();

        hostBuilder.Logging.ClearProviders();
        hostBuilder.Logging.AddNLog();
        hostBuilder.Logging.SetMinimumLevel(settings.LoggingSettings.ToHostLogLevel());

        var services = hostBuilder.Services;
        services.AddSingleton(settings);
        services.AddSingleton<EndpointRegistry>();
        services.AddSingleton<MessageTypeRegistry>();
        services.AddSingleton<EndpointInstanceActivityTracker>();
        services.AddSingleton(sp => buildQueueLengthProvider(sp.GetRequiredService<QueueLengthStore>()));
        services.AddSingleton<LegacyQueueLengthReportHandler.LegacyQueueLengthEndpoints>();

        services.RegisterAsSelfAndImplementedInterfaces<RetriesStore>();
        services.RegisterAsSelfAndImplementedInterfaces<CriticalTimeStore>();
        services.RegisterAsSelfAndImplementedInterfaces<ProcessingTimeStore>();
        services.RegisterAsSelfAndImplementedInterfaces<QueueLengthStore>();

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

        ConfigureEndpoint(endpointConfiguration, onCriticalError, transportCustomization, settings, services);
        hostBuilder.UseNServiceBus(endpointConfiguration);

        hostBuilder.AddAsyncTimer();
    }

    static void ConfigureEndpoint(EndpointConfiguration config, Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError, ITransportCustomization transportCustomization, Settings settings, IServiceCollection services)
    {
        if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
        {
            config.License(settings.LicenseFileText);
        }

        var transportSettings = new TransportSettings
        {
            RunCustomChecks = false,
            ConnectionString = settings.ConnectionString,
            EndpointName = settings.EndpointName,
            MaxConcurrency = settings.MaximumConcurrencyLevel
        };

        transportCustomization.CustomizeMonitoringEndpoint(config, transportSettings);

        var serviceControlThroughputDataQueue = settings.ServiceControlThroughputDataQueue;
        if (!string.IsNullOrWhiteSpace(serviceControlThroughputDataQueue))
        {
            if (serviceControlThroughputDataQueue.IndexOf("@") >= 0)
            {
                serviceControlThroughputDataQueue = serviceControlThroughputDataQueue.Substring(0, serviceControlThroughputDataQueue.IndexOf("@"));
            }

            var routing = new RoutingSettings(config.GetSettings());
            routing.RouteToEndpoint(typeof(RecordEndpointThroughputData), serviceControlThroughputDataQueue);

            services.AddHostedService<ReportThroughputHostedService>();
        }


        if (settings.EnableInstallers)
        {
            config.EnableInstallers(settings.Username);
        }

        config.DefineCriticalErrorAction(onCriticalError);

        config.GetSettings().Set(settings);
        config.SetDiagnosticsPath(settings.LoggingSettings.LogPath);
        config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

        config.UseSerialization<SystemJsonSerializer>();
        config.UsePersistence<NonDurablePersistence>();

        var recoverability = config.Recoverability();
        recoverability.Immediate(c => c.NumberOfRetries(3));
        recoverability.Delayed(c => c.NumberOfRetries(0));

        config.SendFailedMessagesTo(settings.ErrorQueue);

        config.DisableFeature<AutoSubscribe>();

        config.AddDeserializer<TaggedLongValueWriterOccurrenceSerializerDefinition>();
        config.Pipeline.Register(typeof(MessagePoolReleasingBehavior), "Releases pooled message.");
        config.EnableFeature<QueueLength.QueueLength>();

        if (AppEnvironment.RunningInContainer)
        {
            // Do not write diagnostics file
            config.CustomDiagnosticsWriter((_, _) => Task.CompletedTask);
        }
    }

    static Func<QueueLengthStore, IProvideQueueLength> QueueLengthProviderBuilder(string connectionString,
        ITransportCustomization transportCustomization) =>
        qls =>
        {
            var queueLengthProvider = transportCustomization.CreateQueueLengthProvider();

            Action<QueueLengthEntry[], EndpointToQueueMapping> store = (es, q) =>
                qls.Store(es.Select(e => ToEntry(e)).ToArray(), ToQueueId(q));

            queueLengthProvider.Initialize(connectionString, store);

            return queueLengthProvider;
        };

    static EndpointInputQueue ToQueueId(EndpointToQueueMapping endpointInputQueueDto)
        => new EndpointInputQueue(endpointInputQueueDto.EndpointName, endpointInputQueueDto.InputQueue);

    static RawMessage.Entry ToEntry(QueueLengthEntry entryDto) =>
        new RawMessage.Entry { DateTicks = entryDto.DateTicks, Value = entryDto.Value };
}