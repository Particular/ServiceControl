namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.ServiceProcess;
    using Autofac;
    using Hosting;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Installation.Environments;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using ServiceBus.Management.Infrastructure.Settings;
    using LogManager = NLog.LogManager;

    public class Bootstrapper
    {
        IStartableBus bus;
        public static IContainer Container { get; set; }


        public Bootstrapper(ServiceBase host = null, HostArguments hostArguments = null, Configure configure = null)
        {
            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = Settings.HttpDefaultConnectionLimit;

            Settings.ServiceName = DetermineServiceName(host, hostArguments);
            ConfigureLogging();
            var containerBuilder = new ContainerBuilder();
            Container = containerBuilder.Build();

            // Disable Auditing for the service control endpoint
            Configure.Features.Disable<Audit>();
            Configure.Features.Enable<Sagas>();
            Feature.Disable<AutoSubscribe>();
            Feature.Disable<SecondLevelRetries>();

            Configure.Serialization.Json();
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions().DoNotWrapHandlersExecutionInATransactionScope());

            Feature.EnableByDefault<StorageDrivenPublisher>();
            Configure.ScaleOut(s => s.UseSingleBrokerQueue());
            
            var transportType = DetermineTransportType();

            if (configure == null)
            {
                configure = Configure
                    .With(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            bus = configure
                .DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t))
                .DefineEndpointName(Settings.ServiceName)
                .AutofacBuilder(Container)
                .UseTransport(transportType)
                .MessageForwardingInCaseOfFault()
                .DefineCriticalErrorAction((s, exception) =>
                {
                    if (host != null)
                    {
                        host.Stop();
                    }
                })
                .UnicastBus()
                .CreateBus();
        }

        static Type DetermineTransportType()
        {
            var Logger = NServiceBus.Logging.LogManager.GetLogger(typeof(Bootstrapper));
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
             var Logger = NServiceBus.Logging.LogManager.GetLogger(typeof(Bootstrapper));
            if (Settings.MaintenanceMode)
            {
                Logger.InfoFormat("RavenDB is now accepting requests on {0}", Settings.StorageUrl);
                Logger.Warn("RavenDB Maintenance Mode - Press Enter to exit");
                while (Console.ReadLine() == null)
                {
                }
                return;
            }

            
            bus.Start(() =>
            {
                if (Environment.UserInteractive && Debugger.IsAttached)
                {
                    Configure.Instance.ForInstallationOn<Windows>().Install();
                }
            });
        }

        public void Stop()
        {
            bus.Dispose();
        }

        static void ConfigureLogging()
        {
            if (LogManager.Configuration != null)
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

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*",                               LogLevel.Error, fileTarget)  { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.RavenDB.Persistence.*",     LogLevel.Error, fileTarget)  { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*",               LogLevel.Error, fileTarget)  { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info,  fileTarget)  { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*",                               LogLevel.Error, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.RavenDB.Persistence.*",     LogLevel.Error, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*",               LogLevel.Error, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("Particular.ServiceControl.Licensing.*", LogLevel.Info,  consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget)); 
            nlogConfig.AddTarget("console", consoleTarget);

            NLogConfigurator.Configure(new object[] { fileTarget, consoleTarget }, "Info");
            LogManager.Configuration = nlogConfig;

            Settings.Logger = NServiceBus.Logging.LogManager.GetLogger(typeof(Settings));
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