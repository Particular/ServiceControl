namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Principal;
    using System.ServiceProcess;
    using Autofac;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.SignalR;
    using Microsoft.Owin.Hosting;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Hosting;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogLevel = NLog.LogLevel;
    using LogManager = NServiceBus.Logging.LogManager;

    public class Bootstrapper : BootstrapperBase
    {
        public IStartableBus Bus;

        private BusConfiguration configuration;
        ShutdownNotifier notifier = new ShutdownNotifier();
        private Startup startup;
        TimeKeeper timeKeeper;
        private IDisposable webApp;

        public Bootstrapper(ServiceBase host = null, HostArguments hostArguments = null, BusConfiguration configuration = null)
        {
            this.host = host;
            this.configuration = configuration;

            // ServiceName is required to determine the default logging path
            LoggingSettings.ServiceName = DetermineServiceName(host, hostArguments);

            logger = ConfigureLogging();

            Settings.ServiceName = LoggingSettings.ServiceName;

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = Settings.HttpDefaultConnectionLimit;

            timeKeeper = new TimeKeeper();

            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            containerBuilder.RegisterInstance(notifier).ExternallyOwned();
            containerBuilder.RegisterInstance(timeKeeper).ExternallyOwned();
            containerBuilder.RegisterType<SubscribeToOwnEvents>().PropertiesAutowired().SingleInstance();
            containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();

            container = containerBuilder.Build();
            startup = new Startup(container);
        }

        public void Start()
        {
            webApp = WebApp.Start(new StartOptions(Settings.RootUrl), startup.Configuration);

            if (IsRunningAcceptanceTests() || (Environment.UserInteractive && Debugger.IsAttached))
            {
                SetupBootstrapper.CreateDatabase(WindowsIdentity.GetCurrent().Name);
                SetupBootstrapper.InitialiseDatabase();
            }

            Bus = ConfigureNServiceBus(configuration);

            container.Resolve<SubscribeToOwnEvents>().Run();

            Bus.Start();

            logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);
        }

        private bool IsRunningAcceptanceTests()
        {
            return configuration != null;
        }

        public void Stop()
        {
            notifier.Dispose();
            Bus.Dispose();
            timeKeeper.Dispose();
            webApp.Dispose();
            documentStore.Dispose();
        }

        static long DataSize()
        {
            var datafilePath = Path.Combine(Settings.DbPath, "data");

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

        static ILog ConfigureLogging()
        {
            LogManager.Use<NLogFactory>();

            const long megaByte = 1073741824;
            if (NLog.LogManager.Configuration != null)
            {
                return LogManager.GetLogger(typeof(Bootstrapper));
            }

            var version = typeof(Bootstrapper).Assembly.GetName().Version;
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");
            var header = $@"-------------------------------------------------------------
ServiceControl Version:				{version}
Selected Transport:					{Settings.TransportType}
Audit Retention Period:				{Settings.AuditRetentionPeriod}
Error Retention Period:				{Settings.ErrorRetentionPeriod}
Forwarding Error Messages:		{Settings.ForwardErrorMessages}
Forwarding Audit Messages:		{Settings.ForwardAuditMessages}
Database Size:							{DataSize()}bytes
-------------------------------------------------------------";

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(LoggingSettings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(LoggingSettings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize =  30 * megaByte,
                Header = new SimpleLayout(header)
            };


            var ravenFileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(LoggingSettings.LogPath, "ravenlog.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(LoggingSettings.LogPath, "ravenlog.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30 * megaByte,
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
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LoggingSettings.RavenDBLogLevel, ravenFileTarget));
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
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LoggingSettings.LoggingLevel, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LoggingSettings.LoggingLevel < LogLevel.Info ? LoggingSettings.LoggingLevel : LogLevel.Info, consoleTarget));

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
            logger.InfoFormat("Logging to {0} with LoggingLevel '{1}'", fileTarget.FileName.Render(logEventInfo), LoggingSettings.LoggingLevel.Name);
            logger.InfoFormat("RavenDB logging to {0} with LoggingLevel '{1}'", ravenFileTarget.FileName.Render(logEventInfo), LoggingSettings.RavenDBLogLevel.Name);

            return logger;
        }

        static string DetermineServiceName(ServiceBase service, HostArguments hostArguments)
        {
            //if Arguments not null then bootstrapper was run from installer so use servicename passed to the installer
            if (hostArguments != null)
            {
                return hostArguments.ServiceName;
            }

            // Try to get HostName from Windows Service Name, default to "Particular.ServiceControl"
            if (string.IsNullOrWhiteSpace(service?.ServiceName))
            {
                return "Particular.ServiceControl";
            }
            return service.ServiceName;
        }
    }
}
