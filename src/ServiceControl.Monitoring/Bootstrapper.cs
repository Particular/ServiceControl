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
    using Autofac.Features.ResolveAnything;
    using Infrastructure;
    using Licensing;
    using Messaging;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
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

            var buildQueueLengthProvider = QueueLengthProviderBuilder(settings.ConnectionString, transportCustomization);

            var containerBuilder = CreateContainer(settings, buildQueueLengthProvider);

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
                this.onCriticalError(c);
                return TaskEx.Completed;
            });

            config.GetSettings().Set(settings);

            config.UseSerialization<NewtonsoftSerializer>();
            config.UsePersistence<InMemoryPersistence>();
            config.SendFailedMessagesTo(settings.ErrorQueue);
            config.DisableFeature<AutoSubscribe>();

            config.AddDeserializer<TaggedLongValueWriterOccurrenceSerializerDefinition>();
            config.Pipeline.Register(typeof(MessagePoolReleasingBehavior), "Releases pooled message.");
            config.EnableFeature<QueueLength.QueueLength>();

            config.EnableFeature<LicenseCheckFeature>();

            containerBuilder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type => type.Assembly == typeof(Bootstrapper).Assembly && type.GetInterfaces().Any() == false));
            containerBuilder.RegisterInstance(settings);

            RegisterInternalWebApiControllers(containerBuilder);

            container = containerBuilder.Build();

#pragma warning disable CS0618 // Type or member is obsolete
            config.UseContainer<AutofacBuilder>(
#pragma warning restore CS0618 // Type or member is obsolete
                c => c.ExistingLifetimeScope(container)
            );

            Startup = new Startup(container);
        }

        static void RegisterInternalWebApiControllers(ContainerBuilder containerBuilder)
        {
            var controllerTypes = Assembly.GetExecutingAssembly().DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                containerBuilder.RegisterType(controllerType).FindConstructorsWith(new AllConstructorFinder());
            }
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

        static ContainerBuilder CreateContainer(Settings settings, Func<QueueLengthStore, IProvideQueueLength> buildQueueLengthProvider)
        {
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterModule<ApplicationModule>();
            containerBuilder.RegisterInstance(settings).As<Settings>().SingleInstance();
            containerBuilder.Register(c => buildQueueLengthProvider(c.Resolve<QueueLengthStore>())).As<IProvideQueueLength>().SingleInstance();

            return containerBuilder;
        }

        public async Task<BusInstance> Start()
        {
            bus = await Endpoint.Start(configuration);

            StartWebApi();

            return new BusInstance(bus);
        }

        void StartWebApi()
        {
            var startOptions = new StartOptions(settings.RootUrl);

            WebApp = Microsoft.Owin.Hosting.WebApp.Start(startOptions, b => Startup.Configuration(b));
        }

        public async Task Stop()
        {
            if (bus != null)
            {
                await bus.Stop().ConfigureAwait(false);
            }

            WebApp?.Dispose();
            container.Dispose();
        }

        public IDisposable WebApp;
        Action<ICriticalErrorContext> onCriticalError;
        Settings settings;
        EndpointConfiguration configuration;
        IContainer container;
        IEndpointInstance bus;

        static ILog Logger = LogManager.GetLogger<Bootstrapper>();
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