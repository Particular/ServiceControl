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
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Hosting.Helpers;
    using Particular.ServiceControl;
    using ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration;

    public class ManagementEndpointSetup : IEndpointSetupTemplate
    {
        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            var builder = new BusConfiguration();
            var transportToUse = AcceptanceTest.GetTransportIntegrationFromEnvironmentVar();
            builder.TypesToScan(GetTypesScopedByTestClass(transportToUse, endpointConfiguration));
            builder.EnableInstallers();


            Action action = () => transportToUse.OnEndpointShutdown(builder.GetSettings().EndpointName());
            builder.GetSettings().Set("CleanupTransport", action);
            builder.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);

            builder.RegisterComponents(r =>
            {
                r.RegisterSingleton(runDescriptor.ScenarioContext.GetType(), runDescriptor.ScenarioContext);
                r.RegisterSingleton(typeof(ScenarioContext), runDescriptor.ScenarioContext);
            });

            configurationBuilderCustomization(builder);

            var startableBus = new Bootstrapper(configuration: builder).Bus;

            LogManager.Configuration = SetupLogging(endpointConfiguration);

            endpointConfiguration.SelfHost(() => startableBus);


            return builder;
        }

        static IEnumerable<Type> GetTypesScopedByTestClass(ITransportIntegration transportToUse, EndpointConfiguration endpointConfiguration)
        {
            var assemblies = new AssemblyScanner().GetScannableAssemblies();

            var types = assemblies.Assemblies
                //exclude all test types by default
                                  .Where(a => a != Assembly.GetExecutingAssembly())
                                  .Where(a =>
                                  {
                                      if (a == transportToUse.Type.Assembly)
                                      {
                                          return true;
                                      }
                                      return !a.GetName().Name.Contains("Transports");
                                  })
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
                Layout = "${longdate}|${level:uppercase=true}|${threadid}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}"
            };

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

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