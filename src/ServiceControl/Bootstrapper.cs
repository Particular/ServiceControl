namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using Autofac;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.Infrastructure.SignalR;
    using Microsoft.Owin.Hosting;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using Particular.ServiceControl.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogLevel = NLog.LogLevel;
    using LogManager = NServiceBus.Logging.LogManager;

    public class Bootstrapper
    {
        private BusConfiguration configuration;
        private EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private ExposeBus exposeBus;
        private ServiceBase host;
        private ShutdownNotifier notifier = new ShutdownNotifier();
        private Settings settings;
        private TimeKeeper timeKeeper;

        public IDisposable WebApp;

        // Windows Service
        public Bootstrapper(ServiceBase host)
        {
            this.host = host;
            settings = new Settings(host.ServiceName);
            Initialize();
        }

        // MaintCommand
        public Bootstrapper(Settings settings)
        {
            this.settings = settings;
            Initialize();
        }

        // SetupCommand
        public Bootstrapper(HostArguments hostArguments, BusConfiguration configuration)
        {
            this.configuration = configuration;
            settings = new Settings(hostArguments.ServiceName);
            Initialize();
        }

        // Testing
        public Bootstrapper(Settings settings, BusConfiguration configuration, ExposeBus exposeBus)
        {
            this.configuration = configuration;
            this.exposeBus = exposeBus;
            this.settings = settings;
            Initialize();
        }

        public Startup Startup { get; private set; }

        private void Initialize()
        {
            var loggingSettings = new LoggingSettings(settings.ServiceName);
            ConfigureLogging(loggingSettings);

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            timeKeeper = new TimeKeeper();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            containerBuilder.RegisterInstance(loggingSettings);
            containerBuilder.RegisterInstance(settings);
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(timeKeeper).ExternallyOwned();
            containerBuilder.RegisterType<SubscribeToOwnEvents>().PropertiesAutowired().SingleInstance();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();

            Startup = new Startup(containerBuilder.Build(), host, settings, documentStore, configuration, exposeBus);
        }

        public void Start()
        {
            var logger = LogManager.GetLogger(typeof(Bootstrapper));

            if (settings.MaintenanceMode)
            {
                new RavenBootstrapper().StartRaven(documentStore, settings);

                logger.InfoFormat("RavenDB is now accepting requests on {0}", settings.StorageUrl);

                if (Environment.UserInteractive)
                {
                    logger.Warn("RavenDB Maintenance Mode - Press Enter to exit");
                    while (Console.ReadLine() == null)
                    {
                    }
                }

                return;
            }

            var startOptions = new StartOptions(settings.RootUrl);
            WebApp = Microsoft.Owin.Hosting.WebApp.Start(startOptions, Startup.Configuration);

            logger.InfoFormat("Api is now accepting requests on {0}", settings.ApiUrl);
        }

        public void Stop()
        {
            notifier.Dispose();
            WebApp?.Dispose();
            timeKeeper.Dispose();
            documentStore.Dispose();
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

        private void ConfigureLogging(LoggingSettings loggingSettings)
        {
            LogManager.Use<NLogFactory>();

            const long megaByte = 1073741824;
            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var version = typeof(Bootstrapper).Assembly.GetName().Version;
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            var header = $@"-------------------------------------------------------------
ServiceControl Version:				{version}
Selected Transport:					{settings.TransportType}
Audit Retention Period:				{settings.AuditRetentionPeriod}
Error Retention Period:				{settings.ErrorRetentionPeriod}
Forwarding Error Messages:		{settings.ForwardErrorMessages}
Forwarding Audit Messages:		{settings.ForwardAuditMessages}
Database Size:							{DataSize()}bytes
-------------------------------------------------------------";

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(loggingSettings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(loggingSettings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30*megaByte,
                Header = new SimpleLayout(header)
            };


            var ravenFileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(loggingSettings.LogPath, "ravenlog.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(loggingSettings.LogPath, "ravenlog.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30*megaByte,
                Header = new SimpleLayout(header)
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = simpleLayout,
                UseDefaultRowHighlightingRules = true
            };

            var nullTarget = new NullTarget();

            // There lines don't appear to be necessary.  The rules seem to work without implicitly adding the targets?!?
            nlogConfig.AddTarget("console", consoleTarget);
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("raven", ravenFileTarget);
            nlogConfig.AddTarget("bitbucket", nullTarget);

            // Only want to see raven errors
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", loggingSettings.RavenDBLogLevel, ravenFileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Error, consoleTarget)); //Noise reduction - Only RavenDB errors on the console
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Debug, nullTarget)
            {
                Final = true
            }); //Will swallow debug and above messages


            // Always want to see license logging regardless of default logging level
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info, consoleTarget)
            {
                Final = true
            });

            // Defaults
            nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LoggingLevel, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", loggingSettings.LoggingLevel < LogLevel.Info ? loggingSettings.LoggingLevel : LogLevel.Info, consoleTarget));

            // Remove Console Logging when running as a service
            if (!Environment.UserInteractive)
            {
                foreach (var rule in nlogConfig.LoggingRules.Where(p => p.Targets.Contains(consoleTarget)).ToList())
                {
                    nlogConfig.LoggingRules.Remove(rule);
                }
            }

            NLog.LogManager.Configuration = nlogConfig;

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            var logEventInfo = new LogEventInfo
            {
                TimeStamp = DateTime.Now
            };
            logger.InfoFormat("Logging to {0} with LoggingLevel '{1}'", fileTarget.FileName.Render(logEventInfo), loggingSettings.LoggingLevel.Name);
            logger.InfoFormat("RavenDB logging to {0} with LoggingLevel '{1}'", ravenFileTarget.FileName.Render(logEventInfo), loggingSettings.RavenDBLogLevel.Name);
        }
    }
}