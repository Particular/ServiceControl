namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using NLog;
    using NLog.Config;
    using NLog.Filters;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            ServicePointManager.DefaultConnectionLimit = 100;

            NServiceBus.Logging.LogManager.Use<NLogFactory>();

            SetupLogging(endpointConfiguration);
            
            var typesToInclude = new List<Type>();

            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.AuditProcessedMessagesTo("audit");
            typesToInclude.AddRange(endpointConfiguration.GetTypesScopedByTestClass().Concat(new[]
            {
                typeof(RegisterWrappers),
                typeof(SessionCopInBehavior),
                typeof(SessionCopInBehaviorForMainPipe),
                typeof(TraceIncomingBehavior),
                typeof(TraceOutgoingBehavior)
            }));


            builder.TypesToIncludeInScan(typesToInclude);

            // TODO Move to test constraints
//            var transportToUse = AcceptanceTest.GetTransportIntegrationFromEnvironmentVar();

            builder.DisableFeature<AutoSubscribe>();
            builder.EnableInstallers();
            builder.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            
            await builder.DefineTransport(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            builder.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await builder.DefinePersistence(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            builder.RegisterComponents(r =>
            {
                builder.GetSettings().Set("SC.ConfigureComponent", r);
            });
            builder.Pipeline.Register<SessionCopInBehavior.Registration>();
            builder.Pipeline.Register<SessionCopInBehaviorForMainPipe.Registration>();
            builder.Pipeline.Register<TraceIncomingBehavior.Registration>();
            builder.Pipeline.Register<TraceOutgoingBehavior.Registration>();

            builder.GetSettings().Set("SC.ScenarioContext", runDescriptor.ScenarioContext);
            
            configurationBuilderCustomization(builder);

            return builder;
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }


        static void SetupLogging(EndpointCustomizationConfiguration endpointConfiguration)
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
    }
}