namespace ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using Autofac;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using ServiceBus.Management.Infrastructure.Settings;

    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server, IWantCustomLogging, IWantCustomInitialization
    {
        public static IContainer Container { get; set; }

        public void Init()
        {
            ConfigureLogging();

            Container = new ContainerBuilder().Build();

            var transportType = SettingsReader<string>.Read("TransportType", typeof(Msmq).AssemblyQualifiedName);
            Configure.With()
                .AutofacBuilder(Container)
                .UseTransport(Type.GetType(transportType))
                .UnicastBus();

            ConfigureLicense();

            Configure.Serialization.Json();
            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }

        static void ConfigureLicense()
        {
            using (
                var licenseStream =
                    Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceControl.Operations.EndpointConfiguration.License.xml"))
            {
                using (var sr = new StreamReader(licenseStream))
                {
                    Configure.Instance.License(sr.ReadToEnd());
                }
            }
        }

        static void ConfigureLogging()
        {
            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                ArchiveEvery = FileArchivePeriod.Day,
                FileName = "${specialfolder:folder=ApplicationData}/ServiceBus.Management/logs/logfile.txt",
                ArchiveFileName = "${specialfolder:folder=ApplicationData}/ServiceBus.Management/logs/log.{#}.txt",
                ArchiveNumbering = ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 14
            };

            var consoleTarget = new ColoredConsoleTarget
            {
                UseDefaultRowHighlightingRules = true,
            };
            
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            nlogConfig.AddTarget("console", consoleTarget);
            NLogConfigurator.Configure(new object[] {fileTarget, consoleTarget}, "Info");
            LogManager.Configuration = nlogConfig;
        }
    }
}