namespace ServiceBus.Management.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
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

            var typesToInclude = new List<Type>();

            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.AuditProcessedMessagesTo("audit");
            typesToInclude.AddRange(endpointConfiguration.GetTypesScopedByTestClass().Concat(new[]
            {
                typeof(TraceIncomingBehavior),
                typeof(TraceOutgoingBehavior)
            }));

            builder.Pipeline.Register(new StampDispatchBehavior(), "Stamps outgoing messages with session ID");
            builder.Pipeline.Register(new DiscardMessagesBehavior(), "Discards messages based on session ID");
            builder.Pipeline.Register(new TraceIncomingBehavior(endpointConfiguration.EndpointName), "Adds incoming messages to the acceptance test trace");
            builder.Pipeline.Register(new TraceOutgoingBehavior(endpointConfiguration.EndpointName), "Adds outgoing messages to the acceptance test trace");
            
            builder.SendFailedMessagesTo("error");

            // will work on all the cloud transports
            builder.UseSerialization<NewtonsoftSerializer>();

            builder.TypesToIncludeInScan(typesToInclude);

            builder.DisableFeature<AutoSubscribe>();
            builder.EnableInstallers();
            builder.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            await builder.DefineTransport(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            builder.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            builder.GetSettings().Set<ScenarioContext>(runDescriptor.ScenarioContext);
            await builder.DefinePersistence(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic).SetValue(runDescriptor.ScenarioContext, endpointConfiguration.EndpointName);

            configurationBuilderCustomization(builder);

            return builder;
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }
    }
}