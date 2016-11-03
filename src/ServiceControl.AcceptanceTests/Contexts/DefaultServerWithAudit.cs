namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
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
    using ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            endpointConfiguration.AddressOfAuditQueue = Address.Parse("audit");

            ServicePointManager.DefaultConnectionLimit = 100;

            var settings = runDescriptor.Settings;

            NServiceBus.Logging.LogManager.Use<NLogFactory>();

            SetupLogging(endpointConfiguration);

            var transportToUse = AcceptanceTest.GetTransportIntegrationFromEnvironmentVar();
            var types = GetTypesScopedByTestClass(transportToUse, endpointConfiguration).Concat(new[]
            {
                typeof(RegisterWrappers),
                typeof(SessionCopInBehavior),
                typeof(SessionCopInBehaviorForMainPipe)
            });
            var builder = new BusConfiguration();

            builder.UsePersistence<InMemoryPersistence>();
            builder.EndpointName(endpointConfiguration.EndpointName);
            builder.TypesToScan(types);
            builder.CustomConfigurationSource(configSource);
            builder.EnableInstallers();
            builder.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            builder.DefineTransport(transportToUse);
            builder.RegisterComponents(r =>
            {
                r.RegisterSingleton(runDescriptor.ScenarioContext.GetType(), runDescriptor.ScenarioContext);
                r.RegisterSingleton(typeof(ScenarioContext), runDescriptor.ScenarioContext);
            });
            builder.RegisterComponents(r =>
            {
                builder.GetSettings().Set("SC.ConfigureComponent", r);
            });
            builder.Pipeline.Register<SessionCopInBehavior.Registration>();
            builder.Pipeline.Register<SessionCopInBehaviorForMainPipe.Registration>();

            var serializer = settings.GetOrNull("Serializer");

            if (serializer != null)
            {
                builder.UseSerialization(Type.GetType(serializer));
            }

            builder.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);
            builder.GetSettings().Set("SC.ScenarioContext", runDescriptor.ScenarioContext);
            configurationBuilderCustomization(builder);

            return builder;
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
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

            var logLevel = "WARN";

            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFile,
                Layout = "${longdate}|${level:uppercase=true}|${threadid}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}"
            };

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

            LogManager.Configuration = nlogConfig;
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
                Condition = $"level < LogLevel.{logLevel.Name}"
            });

            return rule;
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
                                  .Where(a => a.GetName().Name != "ServiceControl")
                                  .SelectMany(a => a.GetTypes());

            types = types.Union(GetNestedTypeRecursive(transportToUse.GetType(), null));

            var extraConfigForEndpoint = Type.GetType($"{transportToUse.GetType().Namespace}.{transportToUse.Name}.CustomConfigForEndpoints", false);
            if (extraConfigForEndpoint != null)
            {
                types = types.Union(new [] {extraConfigForEndpoint});
            }

            types = types.Union(GetNestedTypeRecursive(endpointConfiguration.BuilderType.DeclaringType, endpointConfiguration.BuilderType));

            types = types.Union(endpointConfiguration.TypesToInclude);

            var typesScopedByTestClass = types.Where(t => !endpointConfiguration.TypesToExclude.Contains(t)).ToList();
            return typesScopedByTestClass;
        }

        static IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
        {
            yield return rootType;

            if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
                yield break;

            foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
            {
                yield return nestedType;
            }
        }
    }
}