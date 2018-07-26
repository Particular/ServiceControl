namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using Features;
    using ServiceBus.Management.AcceptanceTests;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public DefaultServer()
        {
            typesToInclude = new List<Type>();
        }

        public DefaultServer(List<Type> typesToInclude)
        {
            this.typesToInclude = typesToInclude;
        }

        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var types = endpointConfiguration.GetTypesScopedByTestClass();

            typesToInclude.AddRange(types);

            var configuration = new EndpointConfiguration(endpointConfiguration.EndpointName);

            configuration.TypesToIncludeInScan(typesToInclude);
            configuration.EnableInstallers();

            configuration.DisableFeature<TimeoutManager>();

            configuration.Pipeline.Register(new StampDispatchBehavior(), "Stamps outgoing messages with session ID");
            configuration.Pipeline.Register(new DiscardMessagesBehavior(), "Discards messages based on session ID");
            configuration.Pipeline.Register(new TraceIncomingBehavior(endpointConfiguration.EndpointName), "Adds incoming messages to the acceptance test trace");
            configuration.Pipeline.Register(new TraceOutgoingBehavior(endpointConfiguration.EndpointName), "Adds outgoing messages to the acceptance test trace");

            var recoverability = configuration.Recoverability();
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
            configuration.SendFailedMessagesTo("error");

            await configuration.DefineTransport(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            configuration.GetSettings().Set<ScenarioContext>(runDescriptor.ScenarioContext);
            configuration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await configuration.DefinePersistence(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic).SetValue(runDescriptor.ScenarioContext, endpointConfiguration.EndpointName);

            configurationBuilderCustomization(configuration);

            return configuration;
        }

        List<Type> typesToInclude;
    }
}