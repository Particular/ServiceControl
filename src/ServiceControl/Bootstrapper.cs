namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Web.Http.Controllers;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Autofac.Features.ResolveAnything;
    using global::ServiceControl.CompositeViews.Messages;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Recoverability;
    using global::ServiceControl.Transports;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    class Bootstrapper
    {
        // Windows Service
        public Bootstrapper(Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings, Action<ContainerBuilder> additionalRegistrationActions = null)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            this.configuration = configuration;
            this.loggingSettings = loggingSettings;
            this.additionalRegistrationActions = additionalRegistrationActions;
            this.settings = settings;
            Initialize();
        }

        public Startup Startup { get; private set; }

        public Func<HttpClient> HttpClientFactory { get; set; } = () =>
        {
            if (httpClient == null)
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            return httpClient;
        };

        private void Initialize()
        {
            RecordStartup(loggingSettings, configuration);

            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                configuration.License(settings.LicenseFileText);
            }

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            transportCustomization = settings.LoadTransportCustomization();
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type => type.Assembly == typeof(Bootstrapper).Assembly && type.GetInterfaces().Any() == false));

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();

            transportSettings = MapSettings(settings);

            containerBuilder.RegisterInstance(transportSettings).SingleInstance();

            var rawEndpointFactory = new RawEndpointFactory(settings, transportSettings, transportCustomization);
            containerBuilder.RegisterInstance(rawEndpointFactory).AsSelf();

            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            containerBuilder.RegisterInstance(loggingSettings);
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.Register(c => HttpClientFactory);
            containerBuilder.RegisterModule<ApisModule>();
            containerBuilder.Register(c => bus.Bus);

            containerBuilder.RegisterType<EndpointInstanceMonitoring>().SingleInstance();
            containerBuilder.RegisterType<MonitoringDataPersister>().AsImplementedInterfaces().AsSelf().SingleInstance();
            containerBuilder.RegisterType<ErrorIngestionComponent>().SingleInstance();

            RegisterInternalWebApiControllers(containerBuilder);

            additionalRegistrationActions?.Invoke(containerBuilder);

            container = containerBuilder.Build();
            Startup = new Startup(container);

            domainEvents.SetContainer(container);
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

        public async Task<BusInstance> Start(bool isRunningAcceptanceTests = false)
        {
            var logger = LogManager.GetLogger(typeof(Bootstrapper));

            bus = await NServiceBusFactory.CreateAndStart(settings, transportCustomization, transportSettings, loggingSettings, container, documentStore, configuration, isRunningAcceptanceTests)
                .ConfigureAwait(false);

            if (!isRunningAcceptanceTests)
            {
                var startOptions = new StartOptions(settings.RootUrl);

                WebApp = Microsoft.Owin.Hosting.WebApp.Start(startOptions, b => Startup.Configuration(b));
            }

            logger.InfoFormat("Api is now accepting requests on {0}", settings.ApiUrl);

            return bus;
        }

        public async Task Stop()
        {
            notifier.Dispose();
            if (bus != null)
            {
                await bus.Stop().ConfigureAwait(false);
            }

            documentStore.Dispose();
            WebApp?.Dispose();
            container.Dispose();
        }

        private TransportSettings MapSettings(Settings settings)
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

        private long DataSize()
        {
            var datafilePath = Path.Combine(settings.DbPath, "data");

            try
            {
                var info = new FileInfo(datafilePath);

                return info.Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private void RecordStartup(LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Bootstrapper).Assembly.Location).ProductVersion;
            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:             {version}
Audit Retention Period (optional):  {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
Database Size:                      {DataSize()} bytes
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
RavenDB Logging Level:              {loggingSettings.RavenDBLogLevel}
Selected Transport Customization:   {settings.TransportCustomizationType}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = new
                {
                    settings.ApiUrl,
                    settings.DatabaseMaintenancePort,
                    settings.ErrorLogQueue,
                    settings.DataSpaceRemainingThreshold,
                    settings.DisableRavenDBPerformanceCounters,
                    settings.DbPath,
                    settings.ErrorQueue,
                    settings.ForwardErrorMessages,
                    settings.HttpDefaultConnectionLimit,
                    settings.IngestErrorMessages,
                    settings.MaximumConcurrencyLevel,
                    settings.Port,
                    settings.ProcessRetryBatchesFrequency,
                    settings.RemoteInstances,
                    settings.RetryHistoryDepth,
                    settings.RunInMemory,
                    settings.SkipQueueCreation,
                    settings.TransportCustomizationType,
                    settings.AllowMessageEditing
                },
                LoggingSettings = loggingSettings
            });
        }

        public IDisposable WebApp;
        readonly Action<ContainerBuilder> additionalRegistrationActions;
        private EndpointConfiguration configuration;
        private LoggingSettings loggingSettings;
        private EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private ShutdownNotifier notifier = new ShutdownNotifier();
        private Settings settings;
        private IContainer container;
        private BusInstance bus;
        private TransportSettings transportSettings;
        TransportCustomization transportCustomization;
        private static HttpClient httpClient;

        class AllConstructorFinder : IConstructorFinder
        {
            public ConstructorInfo[] FindConstructors(Type targetType)
            {
                var result = Cache.GetOrAdd(targetType, t => t.GetTypeInfo().DeclaredConstructors.ToArray());

                return result.Length > 0 ? result : throw new Exception($"No constructor found for type {targetType.FullName}");
            }

            static readonly ConcurrentDictionary<Type, ConstructorInfo[]> Cache = new ConcurrentDictionary<Type, ConstructorInfo[]>();
        }
    }
}