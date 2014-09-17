namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System.IO;
    using System.Reflection;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using Particular.ServiceControl;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration,
            IConfigurationSource configSource)
        {
            Configure.ScaleOut(_ => _.UseSingleBrokerQueue());

            new Bootstrapper();

            //We need this hack here because by default we include all assemblies minus Plugins but for these tests we need to exclude other tests!
            for (var index = 0; index < Configure.TypesToScan.Count;)
            {
                var type = Configure.TypesToScan[index];

                if (type.Assembly != Assembly.GetExecutingAssembly())
                {
                    index++;
                    continue;
                }

                if (!(type.DeclaringType == endpointConfiguration.BuilderType.DeclaringType ||
                      type.DeclaringType == endpointConfiguration.BuilderType))
                {
                    Configure.TypesToScan.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }

            LogManager.Configuration = SetupLogging(endpointConfiguration);

            return Configure.Instance;
        }

        protected virtual LoggingConfiguration SetupLogging(EndpointConfiguration endpointConfiguration)
        {
            var logDir = ".\\logfiles\\";

            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, endpointConfiguration.EndpointName + ".txt");

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            var logLevel = "INFO";

            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFile,
            };

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, fileTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            NLogConfigurator.Configure(new object[] {fileTarget}, logLevel);
            return nlogConfig;
        }
    }
}