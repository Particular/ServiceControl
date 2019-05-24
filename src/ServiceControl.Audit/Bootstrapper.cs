namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Threading.Tasks;
    using Autofac;
    using Autofac.Features.ResolveAnything;
    using global::ServiceControl.CompositeViews.Messages;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Transports;
    using Microsoft.Owin.Hosting;
    using Nancy;
    using Nancy.ModelBinding;
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
        public Bootstrapper(Action<ICriticalErrorContext> onCriticalError, Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings, Action<ContainerBuilder> additionalRegistrationActions = null)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            this.onCriticalError = onCriticalError;
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
                httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            return httpClient;
        };

        private void Initialize()
        {
            RecordStartup(loggingSettings, configuration);

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            transportCustomization = settings.LoadTransportCustomization();
            var containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type => type.Assembly == typeof(Bootstrapper).Assembly && type.GetInterfaces().Any() == false));

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();

            transportSettings = new TransportSettings();
            containerBuilder.RegisterInstance(transportSettings).SingleInstance();

            containerBuilder.RegisterInstance(loggingSettings);
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.Register(c => HttpClientFactory);
            containerBuilder.RegisterModule<ApisModule>();
            containerBuilder.RegisterType<MessageForwarder>().AsImplementedInterfaces().SingleInstance();
            containerBuilder.Register(c => bus.Bus);

            containerBuilder.RegisterType<DomainEventBusPublisher>().AsImplementedInterfaces().AsSelf().SingleInstance();
            containerBuilder.RegisterType<EndpointInstanceMonitoring>().SingleInstance();

            containerBuilder.RegisterType<ServiceBus.Management.Infrastructure.Nancy.JsonNetSerializer>().As<ISerializer>();
            containerBuilder.RegisterType<ServiceBus.Management.Infrastructure.Nancy.JsonNetBodyDeserializer>().As<IBodyDeserializer>();
            containerBuilder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly()).Where(t => t.IsAssignableTo<INancyModule>()).As<INancyModule>();

            additionalRegistrationActions?.Invoke(containerBuilder);

            container = containerBuilder.Build();
            Startup = new Startup(container);

            domainEvents.SetContainer(container);
        }

        public async Task<BusInstance> Start(bool isRunningAcceptanceTests = false)
        {
            var logger = LogManager.GetLogger(typeof(Bootstrapper));

            bus = await NServiceBusFactory.CreateAndStart(settings, transportCustomization, transportSettings, loggingSettings, container, onCriticalError, documentStore, configuration, isRunningAcceptanceTests)
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
            var version = typeof(Bootstrapper).Assembly.GetName().Version;
            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:             {version}
Audit Retention Period:             {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Ingest Audit Messages:              {settings.IngestErrorMessages}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
Forwarding Audit Messages:          {settings.ForwardAuditMessages}
Database Size:                      {DataSize()} bytes
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
RavenDB Logging Level:              {loggingSettings.RavenDBLogLevel}
Selected Transport Customization:   {settings.TransportCustomizationType}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
            endpointConfiguration.SetDiagnosticsPath(loggingSettings.LogPath);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = new
                {
                    settings.ApiUrl,
                    settings.AuditLogQueue,
                    settings.AuditQueue,
                    settings.DatabaseMaintenancePort,
                    settings.ErrorLogQueue,
                    settings.DisableRavenDBPerformanceCounters,
                    settings.DbPath,
                    settings.ErrorQueue,
                    settings.ForwardAuditMessages,
                    settings.ForwardErrorMessages,
                    settings.HttpDefaultConnectionLimit,
                    settings.IngestAuditMessages,
                    settings.IngestErrorMessages,
                    settings.MaxBodySizeToStore,
                    settings.MaximumConcurrencyLevel,
                    settings.Port,
                    settings.ProcessRetryBatchesFrequency,
                    settings.RemoteInstances,
                    settings.RetryHistoryDepth,
                    settings.RunInMemory,
                    settings.SkipQueueCreation,
                    settings.TransportCustomizationType
                },
                LoggingSettings = loggingSettings
            });
        }

        public IDisposable WebApp;
        private EndpointConfiguration configuration;
        private LoggingSettings loggingSettings;
        readonly Action<ContainerBuilder> additionalRegistrationActions;
        private EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private Action<ICriticalErrorContext> onCriticalError;
        private ShutdownNotifier notifier = new ShutdownNotifier();
        private Settings settings;
        private IContainer container;
        private BusInstance bus;
        private TransportSettings transportSettings;
        TransportCustomization transportCustomization;
        private static HttpClient httpClient;
    }
}