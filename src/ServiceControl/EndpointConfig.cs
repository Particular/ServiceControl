namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using Autofac;
    using NLog;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using ServiceBus.Management.Infrastructure.Settings;

    public class EndpointConfig : IConfigureThisEndpoint, AsA_Publisher, IWantCustomLogging, IWantCustomInitialization
    {
        public static IContainer Container { get; set; }

        public void Init()
        {
            ConfigureLogging();

            var containerBuilder = new ContainerBuilder();

            Container = containerBuilder.Build();
            
            // Disable Auditing for the service control endpoint
            Configure.Features.Disable<Audit>();
            Configure.Features.Enable<Sagas>();

            var transportType = Type.GetType(Settings.TransportType);
            Configure
                .With(AllAssemblies.Except("ServiceControl.Plugin"))
                .AutofacBuilder(Container)
                .UseTransport(transportType)
                .UnicastBus();

            Feature.Disable<AutoSubscribe>();
            Feature.Disable<SecondLevelRetries>();

            Configure.Serialization.Json();
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }

        static void ConfigureLogging()
        {
            var nlogConfig = new LoggingConfiguration();
            var simpleLayout = new SimpleLayout("${longdate}|${level}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}");

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
    }
}
