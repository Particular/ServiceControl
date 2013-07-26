namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System.IO;
    using System.Reflection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Logging.Loggers.Log4NetAdapter;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource)
        {
            SetupLogging(endpointConfiguration);

            Configure.Serialization.Xml();

            return Configure.With(AllAssemblies.Except(Assembly.GetExecutingAssembly().FullName))
                            .DefaultBuilder()
                            .UseTransport<Msmq>()
                            .UnicastBus();
        }

        static void SetupLogging(EndpointConfiguration endpointConfiguration)
        {
            var logDir = ".\\logfiles\\";

            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, endpointConfiguration.EndpointName + ".txt");

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            var logLevel = "INFO";

            SetLoggingLibrary.Log4Net(null, Log4NetAppenderFactory.CreateRollingFileAppender(logLevel, logFile));
        }
    }
}