namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.Hosting.Helpers;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using ServiceControl.MessageAuditing;

    public class DefaultServerWithoutAudit : DefaultServer
    {
        public override void AddMoreConfig()
        {
            Configure.Features.Disable<Audit>();
        }
    }

    public class DefaultServer : IEndpointSetupTemplate
    {
        public virtual void AddMoreConfig()
        {
            
        }

        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration,
            IConfigurationSource configSource)
        {
            var settings = runDescriptor.Settings;

            var types = GetTypesToUse(endpointConfiguration);

            var transportToUse = settings.GetOrNull("Transport");

            SetupLogging(endpointConfiguration);

            Configure.Features.Enable<Sagas>();

            AddMoreConfig();

            var config = Configure.With(types)
                .DefineEndpointName(endpointConfiguration.EndpointName)
                .CustomConfigurationSource(configSource)
                .DefineBuilder(settings.GetOrNull("Builder"))
                .DefineSerializer(settings.GetOrNull("Serializer"))
                .DefineTransport(transportToUse)
                .InMemorySagaPersister();


            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer") ||
                transportToUse.Contains("RabbitMq"))
            {
                config.UseInMemoryTimeoutPersister();
            }

            if (transportToUse == null || transportToUse.Contains("Msmq") || transportToUse.Contains("SqlServer"))
            {
                config.InMemorySubscriptionStorage();
            }

            config.InMemorySagaPersister();

            return config.UnicastBus();
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

            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFile,
            };

            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            NLogConfigurator.Configure(new object[] {fileTarget}, logLevel);
            LogManager.Configuration = nlogConfig;
        }

        static IEnumerable<Type> GetTypesToUse(EndpointConfiguration endpointConfiguration)
        {
            var assemblies = new AssemblyScanner().GetScannableAssemblies().Assemblies
                .Where(a => a != typeof(ProcessedMessage).Assembly).ToList();


            var types = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(
                    t =>
                        t.Assembly != Assembly.GetExecutingAssembly() || //exclude all test types by default
                        t.DeclaringType == endpointConfiguration.BuilderType.DeclaringType ||
                        //but include types on the test level
                        t.DeclaringType == endpointConfiguration.BuilderType);
                //and the specific types for this endpoint
            return types;
        }
    }
}