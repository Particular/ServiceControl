namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Autofac;
    using global::ServiceControl.CompositeViews.Messages;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Monitoring;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogManager = NServiceBus.Logging.LogManager;

    public class Bootstrapper
    {
        private static HttpClient httpClient;
        private BusConfiguration configuration;
        private LoggingSettings loggingSettings;
        readonly ComponentActivator[] components;
        private EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private Action onCriticalError;
        private ShutdownNotifier notifier = new ShutdownNotifier();
        private Settings settings;
        private TimeKeeper timeKeeper;
        private IContainer container;
        private BusInstance bus;
        public IDisposable WebApp;

        // Windows Service
        public Bootstrapper(Action onCriticalError, Settings settings, BusConfiguration configuration, LoggingSettings loggingSettings, ComponentActivator[] components)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            this.onCriticalError = onCriticalError;
            this.configuration = configuration;
            this.loggingSettings = loggingSettings;
            this.components = components;
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
            RecordStartup(loggingSettings);

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            timeKeeper = new TimeKeeper();

            var containerBuilder = new ContainerBuilder();

            var domainEvents = new DomainEvents();
            containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();
            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            containerBuilder.RegisterInstance(loggingSettings);
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(timeKeeper).AsSelf().AsImplementedInterfaces().ExternallyOwned();
            containerBuilder.RegisterType<SubscribeToOwnEvents>().PropertiesAutowired().SingleInstance();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
            containerBuilder.RegisterType<EndpointUptimeInformationPersister>().AsImplementedInterfaces().SingleInstance();
            containerBuilder.Register(c => HttpClientFactory);
            containerBuilder.RegisterModule<ApisModule>();

            foreach (var component in components)
            {
                component.RegisterDependency(containerBuilder);

                var parts = component.CreateParts();

                foreach (var part in parts)
                {
                    containerBuilder.RegisterInstance(part).ExternallyOwned().AsImplementedInterfaces().AsSelf();
                }
            }

            container = containerBuilder.Build();
            Startup = new Startup(container);

            domainEvents.SetContainer(container);
        }

        public BusInstance Start(bool isRunningAcceptanceTests = false)
        {
            var logger = LogManager.GetLogger(typeof(Bootstrapper));

            if (!isRunningAcceptanceTests)
            {
                var startOptions = new StartOptions(settings.RootUrl);

                WebApp = Microsoft.Owin.Hosting.WebApp.Start(startOptions, b => Startup.Configuration(b));
            }

            RavenBootstrapper.StartRaven(documentStore, settings, false);

            foreach (var component in components)
            {
                component.Initialize(container).GetAwaiter().GetResult();
            }

            bus = NServiceBusFactory.CreateAndStart(settings, container, onCriticalError, documentStore, configuration, isRunningAcceptanceTests);

            logger.InfoFormat("Api is now accepting requests on {0}", settings.ApiUrl);

            return bus;
        }

        public void Stop()
        {
            notifier.Dispose();
            bus?.Dispose();
            timeKeeper.Dispose();
            documentStore.Dispose();
            WebApp?.Dispose();
            container.Dispose();

            foreach (var component in components)
            {
                component.TearDown().GetAwaiter().GetResult();
            }
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

        private void RecordStartup(LoggingSettings loggingSettings)
        {
            var version = typeof(Bootstrapper).Assembly.GetName().Version;
            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Version:       {version}
Selected Transport:           {settings.TransportType}
Audit Retention Period:       {settings.AuditRetentionPeriod}
Error Retention Period:       {settings.ErrorRetentionPeriod}
Forwarding Error Messages:    {settings.ForwardErrorMessages}
Forwarding Audit Messages:    {settings.ForwardAuditMessages}
Database Size:                {DataSize()}bytes
ServiceControl Logging Level: {loggingSettings.LoggingLevel}
RavenDB Logging Level:        {loggingSettings.RavenDBLogLevel}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
        }
    }
}