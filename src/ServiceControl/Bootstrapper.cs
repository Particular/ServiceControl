namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using Autofac;
    using Nancy.Security;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceBus.Management.Infrastructure.Settings;

    public class Bootstrapper
    {
        IStartableBus bus;
        public static IContainer Container { get; set; }

        public Bootstrapper(ServiceBase host,bool runInstallers=false,string username = null)
        {
            Settings.ServiceName = DetermineServiceName(host);
            ConfigureLogging();
            var containerBuilder = new ContainerBuilder();
            
            Container = containerBuilder.Build();


            var busConfiguration = new BusConfiguration();

            busConfiguration.UseContainer<Autofac>(c => c.ExistingLifetimeScope(Container));

            busConfiguration.DisableFeature<Audit>();
            busConfiguration.DisableFeature<AutoSubscribe>();
            busConfiguration.DisableFeature<SecondLevelRetries>();

            busConfiguration.UseSerialization<JsonSerializer>();
            busConfiguration.Transactions().DisableDistributedTransactions();
            busConfiguration.ScaleOut().UseSingleBrokerQueue();
            var transportType = Type.GetType(Settings.TransportType);

            busConfiguration.UseTransport(transportType);
            busConfiguration.EndpointName(Settings.ServiceName);
            busConfiguration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));

            busConfiguration.DefineCriticalErrorAction((s, exception) =>
            {
                if (host != null)
                {
                    host.Stop();
                }
            });


            if (runInstallers)
            {
                busConfiguration.EnableInstallers(username);    
            }
            

            bus = Bus.Create(busConfiguration);
        }

        public void Start()
        {
            bus.Start();
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

            //todo: ref nlog adapter
            //NLogConfigurator.Configure(new object[] { fileTarget, consoleTarget }, "Info"); 
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
