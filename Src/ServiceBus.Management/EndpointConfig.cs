namespace ServiceBus.Management
{
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using System;
    using System.IO;
    using System.Reflection;
    using NServiceBus;

    public class EndpointConfig : IConfigureThisEndpoint, AsA_Server, IWantCustomLogging, IWantCustomInitialization
    {
        public void Init()
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
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            NLogConfigurator.Configure(fileTarget, "Info");
            LogManager.Configuration = nlogConfig;

            var transportType = SettingsReader<string>.Read("TransportType", typeof (Msmq).AssemblyQualifiedName);
            Configure.With().UseTransport(Type.GetType(transportType));

            using (var licenseStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServiceBus.Management.License.xml"))
            using (var sr = new StreamReader(licenseStream))
            {
                Configure.Instance.License(sr.ReadToEnd());
            }

            Configure.Transactions.Advanced(t => t.DisableDistributedTransactions());
        }
    }
}