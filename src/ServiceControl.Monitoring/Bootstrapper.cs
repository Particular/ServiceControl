namespace ServiceControl.Monitoring
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Audit.Infrastructure.WebApi;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Infrastructure;
    using Licensing;
    using Messaging;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Pipeline;
    using QueueLength;
    using Timings;
    using Transports;

    public class Bootstrapper
    {
        // Windows Service
        public Bootstrapper(Action<ICriticalErrorContext> onCriticalError, Settings settings, EndpointConfiguration endpointConfiguration)
        {
            this.onCriticalError = onCriticalError;
            this.settings = settings;
            this.endpointConfiguration = endpointConfiguration;

            CreateHost();
        }

        public IHostBuilder HostBuilder { get; private set; }

        void CreateHost()
        {
            var transportCustomization = settings.LoadTransportCustomization();
            var buildQueueLengthProvider = QueueLengthProviderBuilder(settings.ConnectionString, transportCustomization);

            HostBuilder = new HostBuilder();
            HostBuilder
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    //HINT: configuration used by NLog comes from MonitorLog.cs
                    builder.AddNLog();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(settings);
                    services.AddSingleton<LicenseCheckFeatureStartup>();
                    services.AddSingleton<EndpointRegistry>();
                    services.AddSingleton<MessageTypeRegistry>();
                    services.AddSingleton<EndpointInstanceActivityTracker>();
                    services.AddSingleton(sp => buildQueueLengthProvider(sp.GetRequiredService<QueueLengthStore>()));
                })
                .UseNServiceBus(builder =>
                {
                    ConfigureEndpoint(endpointConfiguration);

                    return endpointConfiguration;
                })
                .UseWebApi(settings.RootUrl, settings.ExposeApi);

            HostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory(containerBuilder =>
                {
                    // HINT: There's no good way to do .AsImplementedInterfaces().AsSelf() with IServiceCollection
                    containerBuilder.RegisterType<RetriesStore>().AsImplementedInterfaces().AsSelf().SingleInstance();
                    containerBuilder.RegisterType<CriticalTimeStore>().AsImplementedInterfaces().AsSelf().SingleInstance();
                    containerBuilder.RegisterType<ProcessingTimeStore>().AsImplementedInterfaces().AsSelf().SingleInstance();
                    containerBuilder.RegisterType<QueueLengthStore>().AsImplementedInterfaces().AsSelf().SingleInstance();
                }));
        }

        internal void ConfigureEndpoint(EndpointConfiguration config)
        {
            var transportCustomization = settings.LoadTransportCustomization();

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

            transportCustomization.CustomizeForMonitoringIngestion(config, transportSettings);

            if (settings.EnableInstallers)
            {
                config.EnableInstallers(settings.Username);
            }

            config.DefineCriticalErrorAction(c =>
            {
                onCriticalError(c);
                return TaskEx.Completed;
            });

            config.GetSettings().Set(settings);

            config.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            config.UseSerialization<NewtonsoftSerializer>();
            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo(settings.ErrorQueue);
            config.DisableFeature<AutoSubscribe>();

            config.AddDeserializer<TaggedLongValueWriterOccurrenceSerializerDefinition>();
            config.Pipeline.Register(typeof(MessagePoolReleasingBehavior), "Releases pooled message.");
            config.EnableFeature<QueueLength.QueueLength>();

            config.EnableFeature<LicenseCheckFeature>();
        }

        static Func<QueueLengthStore, IProvideQueueLength> QueueLengthProviderBuilder(string connectionString, TransportCustomization transportCustomization)
        {
            return qls =>
            {
                var queueLengthProvider = transportCustomization.CreateQueueLengthProvider();

                Action<QueueLengthEntry[], EndpointToQueueMapping> store = (es, q) => qls.Store(es.Select(e => ToEntry(e)).ToArray(), ToQueueId(q));

                queueLengthProvider.Initialize(connectionString, store);

                return queueLengthProvider;
            };
        }

        static EndpointInputQueue ToQueueId(EndpointToQueueMapping endpointInputQueueDto)
        {
            return new EndpointInputQueue(endpointInputQueueDto.EndpointName, endpointInputQueueDto.InputQueue);
        }

        static RawMessage.Entry ToEntry(QueueLengthEntry entryDto)
        {
            return new RawMessage.Entry
            {
                DateTicks = entryDto.DateTicks,
                Value = entryDto.Value
            };
        }

        Action<ICriticalErrorContext> onCriticalError;
        Settings settings;

        readonly EndpointConfiguration endpointConfiguration;
    }

    class MessagePoolReleasingBehavior : Behavior<IIncomingLogicalMessageContext>
    {
        public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            try
            {
                await next().ConfigureAwait(false);
            }
            finally
            {
                var messageType = context.Message.MessageType;
                var instance = context.Message.Instance;

                if (messageType == typeof(TaggedLongValueOccurrence))
                {
                    ReleaseMessage<TaggedLongValueOccurrence>(instance);
                }
            }
        }

        static void ReleaseMessage<T>(object instance) where T : RawMessage, new()
        {
            RawMessage.Pool<T>.Default.Release((T)instance);
        }
    }
}