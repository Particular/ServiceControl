namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Web.Http.Controllers;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Http.Diagrams;
    using Infrastructure;
    using Infrastructure.OWIN;
    using Licensing;
    using Messaging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Pipeline;
    using QueueLength;
    using ServiceBus.Management.Infrastructure.OWIN;
    using Transports;
    using Module = Autofac.Module;

    public class Bootstrapper
    {
        // Windows Service
        public Bootstrapper(Action<ICriticalErrorContext> onCriticalError, Settings settings, EndpointConfiguration configuration)
        {
            this.onCriticalError = onCriticalError;
            this.settings = settings;
            this.configuration = configuration;

            Initialize(configuration);
        }

        internal Startup Startup { get; set; }

        void Initialize(EndpointConfiguration config)
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

        public static Func<QueueLengthStore, IProvideQueueLength> QueueLengthProviderBuilder(string connectionString, TransportCustomization transportCustomization)
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

        public async Task<BusInstance> Start()
        {
            bus = await Endpoint.Start(configuration);

            return new BusInstance(bus);
        }

        public async Task Stop()
        {
            if (bus != null)
            {
                await bus.Stop().ConfigureAwait(false);
            }

            WebApp?.Dispose();
        }

        public IDisposable WebApp;
        Action<ICriticalErrorContext> onCriticalError;
        Settings settings;
        EndpointConfiguration configuration;
        IEndpointInstance bus;
    }

    class AllConstructorFinder : IConstructorFinder
    {
        public ConstructorInfo[] FindConstructors(Type targetType)
        {
            var result = Cache.GetOrAdd(targetType, t => t.GetTypeInfo().DeclaredConstructors.ToArray());

            return result.Length > 0 ? result : throw new Exception($"No constructor found for type {targetType.FullName}");
        }

        static readonly ConcurrentDictionary<Type, ConstructorInfo[]> Cache = new ConcurrentDictionary<Type, ConstructorInfo[]>();
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

    class ApiControllerModule : Module
    {
        protected override void Load(ContainerBuilder builder) =>
            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal))
                .AsSelf()
                .InstancePerLifetimeScope()
                .FindConstructorsWith(new AllConstructorFinder());
    }

    class ApplicationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(Include)
                .AsSelf()
                .AsImplementedInterfaces()
                .SingleInstance();
        }

        static bool Include(Type type)
        {
            if (IsMessageType(type))
            {
                return false;
            }

            if (IsMessageHandler(type))
            {
                return false;
            }

            //HINT: DiagramApiController needs per-scope registration and hosted services should have
            //      explicit registration

            if (type == typeof(DiagramApiController) ||
                type == typeof(WebApiHostedService))
            {
                return false;
            }

            return true;
        }

        static bool IsMessageType(Type type)
        {
            return typeof(IMessage).IsAssignableFrom(type);
        }


        static bool IsMessageHandler(Type type)
        {
            return type.GetInterfaces()
                .Where(@interface => @interface.IsGenericType)
                .Select(@interface => @interface.GetGenericTypeDefinition())
                .Any(genericTypeDef => genericTypeDef == typeof(IHandleMessages<>));
        }
    }
}