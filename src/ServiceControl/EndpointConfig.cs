namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using Autofac;
    using NLog;
    using NLog.Config;
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

            var transportType = SettingsReader<string>.Read("TransportType", typeof(Msmq).AssemblyQualifiedName);

            // Disable Auditing for the service control endpoint
            Configure.Features.Disable<Audit>();
            Configure.Features.Enable<Sagas>();

            Configure
                .With(AllAssemblies.Except("ServiceControl.Plugin"))
                .AutofacBuilder(Container)
                .UseTransport(Type.GetType(transportType))
                .UnicastBus();

            ConfigureLicense();

            Feature.Disable<AutoSubscribe>();
            Feature.Disable<SecondLevelRetries>();

            Configure.Serialization.Json();
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }

        static void ConfigureLicense()
        {
            using (var licenseStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceControl.License.xml"))
            using (var sr = new StreamReader(licenseStream))
            {
                Configure.Instance.License(sr.ReadToEnd());
            }
        }

        static void ConfigureLogging()
        {
            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = Settings.LogPath + "/logfile.txt",
                ArchiveFileName = Settings.LogPath + "/log.{#}.txt",
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 14
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                UseDefaultRowHighlightingRules = true,
            };

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*", LogLevel.Error, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("NServiceBus.Licensing.*", LogLevel.Error, consoleTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("console", consoleTarget);
            NLogConfigurator.Configure(new object[] { fileTarget, consoleTarget }, "Info");
            LogManager.Configuration = nlogConfig;
        }
    }
}