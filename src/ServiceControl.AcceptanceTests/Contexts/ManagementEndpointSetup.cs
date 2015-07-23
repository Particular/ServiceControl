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
    using NServiceBus.Hosting.Helpers;
    using NServiceBus.Logging.Loggers.NLogAdapter;
    using Particular.ServiceControl;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public Configure GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration,
            IConfigurationSource configSource)
        {
            Configure.ScaleOut(_ => _.UseSingleBrokerQueue());

            var configure = Configure.With(GetTypesScopedByTestClass(endpointConfiguration));
            new Bootstrapper(configure: configure);

            LogManager.Configuration = SetupLogging(endpointConfiguration);

            return Configure.Instance;
        }

        static IEnumerable<Type> GetTypesScopedByTestClass(EndpointConfiguration endpointConfiguration)
        {
            var assemblies = new AssemblyScanner().GetScannableAssemblies();

            var types = assemblies.Assemblies
                //exclude all test types by default
                                  .Where(a => a != Assembly.GetExecutingAssembly())
                                  .Where(a => !a.GetName().Name.StartsWith("ServiceControl.Plugin"))
                                  .SelectMany(a => a.GetTypes());


            types = types.Union(GetNestedTypeRecursive(endpointConfiguration.BuilderType.DeclaringType, endpointConfiguration.BuilderType));

            types = types.Union(endpointConfiguration.TypesToInclude);

            return types;
        }

        static IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
        {
            if (rootType == null)
            {
                yield break;
            }

            yield return rootType;

            if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
                yield break;

            foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
            {
                yield return nestedType;
            }
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
                Layout = "${longdate}|${level:uppercase=true}|${threadid}|${logger}|${message}${onexception:inner=${newline}${exception}${newline}${stacktrace:format=DetailedFlat}}"
            };

            nlogConfig.LoggingRules.Add(new LoggingRule("Raven.*", LogLevel.Warn, fileTarget) { Final = true });
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);
            NLogConfigurator.Configure(new object[] {fileTarget}, logLevel);
            return nlogConfig;
        }
    }
}