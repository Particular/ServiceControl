namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.ServiceProcess;
    using Autofac;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.SignalR;
    using Microsoft.Owin.Hosting;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Hosting;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Client.Embedded;
    using Raven.Database.Config;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogLevel = NLog.LogLevel;

    public class Bootstrapper
    {
        private readonly ServiceBase host;
        private BusConfiguration configuration;
        private IContainer container;
        EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();
        private ILog logger;
        ShutdownNotifier notifier = new ShutdownNotifier();
        private Startup startup;
        TimeKeeper timeKeeper;
        private IDisposable webApp;

        public IStartableBus Bus;

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

        private void ConfigureNServiceBus()
        {
            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set("ServiceControl.EmbeddableDocumentStore", documentStore);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<SecondLevelRetries>();
            configuration.DisableFeature<TimeoutManager>();

            configuration.UseSerialization<JsonSerializer>();

            configuration.Transactions()
                .DisableDistributedTransactions()
                .DoNotWrapHandlersExecutionInATransactionScope();

            configuration.ScaleOut().UseSingleBrokerQueue();

            var transportType = DetermineTransportType();

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            configuration.EndpointName(Settings.ServiceName);
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            configuration.UseTransport(transportType);
            configuration.DefineCriticalErrorAction((s, exception) =>
            {
                host?.Stop();
            });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            Bus = NServiceBus.Bus.Create(configuration);

            container.Resolve<SubscribeToOwnEvents>().Run();
        }

        Type DetermineTransportType()
        {
            var transportType = Type.GetType(Settings.TransportType);
            if (transportType != null)
            {
                return transportType;
            }
            var errorMsg = $"Configuration of transport Failed. Could not resolve type '{Settings.TransportType}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings";
            logger.Error(errorMsg);
            throw new Exception(errorMsg);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }

        public void Start()
        {
            if (Settings.MaintenanceMode)
            {
                try
                {
                    webApp = WebApp.Start(new StartOptions(Settings.ApiUrl), startup.ConfigureRavenDB);
                    LinkExistingDatabase();

                    logger.InfoFormat("RavenDB is now accepting requests on {0}", Settings.StorageUrl);
                    logger.Warn("RavenDB Maintenance Mode - Press Enter to exit");
                    Console.ReadLine();
                }
                finally
                {
                    Stop();
                }

                return;
            }

            webApp = WebApp.Start(new StartOptions(Settings.ApiUrl), startup.Configuration);

            LinkExistingDatabase();

            ConfigureNServiceBus();
            Bus.Start();

            logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);
        }

        private static void LinkExistingDatabase()
        {
            using (var documentStore = new DocumentStore
            {
                Url = Settings.ApiUrl + "storage"
            }.Initialize())
            {
                try
                {
                    documentStore.DatabaseCommands.GlobalAdmin.CreateDatabase(new DatabaseDocument
                    {
                        Id = "ServiceControl",
                        Settings =
                        {
                            {"Raven/StorageTypeName", InMemoryRavenConfiguration.EsentTypeName},
                            {"Raven/DataDir", Path.Combine(Settings.DbPath, "Databases", "ServiceControl")},
                            {"Raven/Counters/DataDir", Path.Combine(Settings.DbPath, "Data", "Counters")},
                            {"Raven/WebDir", Path.Combine(Settings.DbPath, "Raven", "WebUI")},
                            {"Raven/PluginsDirectory", Path.Combine(Settings.DbPath, "Plugins")},
                            {"Raven/AssembliesDirectory", Path.Combine(Settings.DbPath, "Assemblies")},
                            {"Raven/CompiledIndexCacheDirectory", Path.Combine(Settings.DbPath, "CompiledIndexes")},
                            {"Raven/FileSystem/DataDir", Path.Combine(Settings.DbPath, "FileSystems")},
                            {"Raven/FileSystem/IndexStoragePath", Path.Combine(Settings.DbPath, "FileSystems", "Indexes")}
                        }
                    });
                    Console.Out.WriteLine("Database linked");
                }
                catch (Exception)
                {
                    Console.Out.WriteLine("Database already linked");
                }
            }
        }

        public void Stop()
        {
            notifier.Dispose();
            Bus.Dispose();
            timeKeeper.Dispose();
            webApp.Dispose();
            documentStore.Dispose();
        }

        static ILog ConfigureLogging()
        {
            LogManager.Use<NLogFactory>();

            const long megaByte = 1073741824;
            if (NLog.LogManager.Configuration != null)
            {
                return LogManager.GetLogger(typeof(Bootstrapper));
            }

            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(LoggingSettings.LogPath, "logfile.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(LoggingSettings.LogPath, "logfile.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30*megaByte
            };

            var ravenFileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(LoggingSettings.LogPath, "ravenlog.${shortdate}.txt"),
                ArchiveFileName = Path.Combine(LoggingSettings.LogPath, "ravenlog.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
                ArchiveAboveSize = 30*megaByte
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
            var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
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
