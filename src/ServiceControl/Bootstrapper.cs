namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceProcess;
    using Autofac;
    using global::ServiceControl.Infrastructure.SignalR;
    using NLog.Config;
    using NLog.Filters;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogLevel = NLog.LogLevel;

    public class Bootstrapper
    {
        IStartableBus bus;
        public static IContainer Container { get; set; }

        public IStartableBus Bus
        {
            get { return bus; }
        }

        public Bootstrapper(ServiceBase host = null, HostArguments hostArguments = null, BusConfiguration configuration = null)
        {
            Settings.ServiceName = DetermineServiceName(host, hostArguments);
            ConfigureLogging();
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
            Container = containerBuilder.Build();

            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.EnableFeature<Sagas>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<SecondLevelRetries>();

            configuration.UseSerialization<JsonSerializer>();

            configuration.Transactions()
                .DisableDistributedTransactions()
                .DoNotWrapHandlersExecutionInATransactionScope();

            configuration.EnableFeature<StorageDrivenPublishing>();
            
            configuration.ScaleOut().UseSingleBrokerQueue();
            
            var transportType = DetermineTransportType();

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) && IsExternalContract(t));
            configuration.EndpointName(Settings.ServiceName);
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(Container));
            configuration.UseTransport(transportType);
            configuration.DefineCriticalErrorAction((s, exception) =>
            {
                if (host != null)
                {
                    host.Stop();
                }
            });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            bus = NServiceBus.Bus.Create(configuration);
        }

        static Type DetermineTransportType()
        {
            var Logger = LogManager.GetLogger(typeof(Bootstrapper));
            var transportType = Type.GetType(Settings.TransportType);
            if (transportType != null)
            {
                return transportType;
            }
            var errorMsg = string.Format("Configuration of transport Failed. Could not resolve type '{0}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings", Settings.TransportType);
            Logger.Error(errorMsg);
            throw new Exception(errorMsg);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }

        public void Start()
        {
             var Logger = LogManager.GetLogger(typeof(Bootstrapper));
            if (Settings.MaintenanceMode)
            {
                Logger.InfoFormat("RavenDB is now accepting requests on {0}", Settings.StorageUrl);
                Logger.Warn("RavenDB Maintenance Mode - Press Enter to exit");
                while (Console.ReadLine() == null)
                {
                }
                return;
            }
            
            Bus.Start();
        }

        public void Stop()
        {
            Bus.Dispose();
        }

        static void ConfigureLogging()
        {
            LogManager.Use<NLogFactory>();

            if (NLog.LogManager.Configuration != null)
            {
                return;
            }

            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${threadid}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Path.Combine(Settings.LogPath, "logfile.txt"),
                ArchiveFileName = Path.Combine(Settings.LogPath, "log.{#}.txt"),
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                Layout = simpleLayout,
                MaxArchiveFiles = 14,
            };
            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = simpleLayout,
                UseDefaultRowHighlightingRules = true,
            };

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "NServiceBus.RavenDB.Persistence.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "NServiceBus.Licensing.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Info, "Particular.ServiceControl.Licensing.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(consoleTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(consoleTarget, LogLevel.Error, "NServiceBus.RavenDB.Persistence.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(consoleTarget, LogLevel.Error, "NServiceBus.Licensing.*"));
            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(consoleTarget, LogLevel.Info, "Particular.ServiceControl.Licensing.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget)); 
            nlogConfig.AddTarget("console", consoleTarget);

            NLog.LogManager.Configuration = nlogConfig;

            Settings.Logger = LogManager.GetLogger(typeof(Settings));
        }

        private static LoggingRule MakeFilteredLoggingRule(Target target, LogLevel logLevel, string text)
        {
            var rule = new LoggingRule(text, LogLevel.Info, target)
            {
                Final = true
            };

            rule.Filters.Add(new ConditionBasedFilter
            {
                Action = FilterResult.Ignore, 
                Condition = string.Format("level < LogLevel.{0}", logLevel.Name)
            });

            return rule;
        }
   
        string DetermineServiceName(ServiceBase host, HostArguments hostArguments)
        {
            //if Arguments not null then bootstrapper was run from installer so use servicename passed to the installer
            if (hostArguments != null)
            {
                return hostArguments.ServiceName;
            }

            // Try to get HostName from Windows Service Name, default to "Particular.ServiceControl"
            if ((host == null) || (string.IsNullOrWhiteSpace(host.ServiceName)))
            {
                return "Particular.ServiceControl";
            }
            return host.ServiceName;
        }
    }
}