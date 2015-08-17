namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NLog;
    using NLog.Config;
    using NLog.Filters;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Hosting.Helpers;
    using Particular.ServiceControl;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            var builder = new BusConfiguration();
            builder.TypesToScan(GetTypesScopedByTestClass(endpointConfiguration));
            builder.EnableInstallers();

            var transportToUse = AcceptanceTest.GetTransportIntegrationFromEnvironmentVar();

            Action action = () => transportToUse.OnEndpointShutdown(builder.GetSettings().EndpointName());
            builder.GetSettings().Set("CleanupTransport", action);
            builder.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);

            var startableBus = new Bootstrapper(configuration: builder).Bus;

            endpointConfiguration.SelfHost(() => startableBus);

            LogManager.Configuration = SetupLogging(endpointConfiguration);

            return builder;
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

            var consoleTarget = new ColoredConsoleTarget
            {
                Layout = "${longdate}|${level:uppercase=true}|${threadid}|${logger}|${message}${onexception:inner=${newline}${exception}${newline}${stacktrace:format=DetailedFlat}}",
                UseDefaultRowHighlightingRules = true,
            };

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(consoleTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, consoleTarget));
            nlogConfig.AddTarget("console", consoleTarget);

            return nlogConfig;
        }

        private static LoggingRule MakeFilteredLoggingRule(Target target, LogLevel logLevel, string text)
        {
            var rule = new LoggingRule(text, LogLevel.Info, target)
            {
                Final = true
            };

            rule.Filters.Add(new ConditionBasedFilter
            {
                Action = FilterResult.Ignore,
                Condition = string.Format("level < LogLevel.{0}", logLevel.Name)
            });

            return rule;
        }
    }
}