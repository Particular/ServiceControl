namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.ServiceProcess;
    using Autofac;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Installation.Environments;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using ServiceBus.Management.Infrastructure.Settings;

    public class Bootstrapper
    {
        IStartableBus bus;
        public static IContainer Container { get; set; }

        public Bootstrapper(ServiceBase host = null)
        {
            Settings.ServiceName = "foo";  // DetermineServiceName(host);
            ConfigureLogging();
            var containerBuilder = new ContainerBuilder();
            
            Container = containerBuilder.Build();
            
            // Disable Auditing for the service control endpoint
            Configure.Features.Disable<Audit>();
            Configure.Features.Enable<Sagas>();
            Feature.Disable<AutoSubscribe>();
            Feature.Disable<SecondLevelRetries>();

            Configure.Serialization.Json();
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());

            Feature.EnableByDefault<StorageDrivenPublisher>();
            Configure.ScaleOut(s => s.UseSingleBrokerQueue());
            var transportType = Type.GetType(Settings.TransportType);
            bus = Configure
                .With(AllAssemblies.Except("ServiceControl.Plugin"))
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

        public void Start()
        {
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

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*", LogLevel.Error, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*", LogLevel.Error, consoleTarget) { Final = true });

            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Warn, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget)); 
            
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("console", consoleTarget);
            NLogConfigurator.Configure(new object[] { fileTarget, consoleTarget }, "Info");
            LogManager.Configuration = nlogConfig;
        }
   
        string DetermineServiceName(ServiceBase host)
        {
            if ((host == null) || (string.IsNullOrWhiteSpace(host.ServiceName)))
            {
                return "Particular.ServiceControl";
            }
            return host.ServiceName;
        }
    }
}
