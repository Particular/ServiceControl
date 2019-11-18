namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Features;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceControl.Audit.Infrastructure;

   
    
    public class DefaultServer : IEndpointSetupTemplate
    {
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            ServicePointManager.DefaultConnectionLimit = 100;

            typesToInclude = new List<Type>();

            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);

            typesToInclude.AddRange(endpointConfiguration.GetTypesScopedByTestClass<Bootstrapper>().Concat(new[]
            {
                typeof(TraceIncomingBehavior),
                typeof(TraceOutgoingBehavior)
            }));

            builder.Pipeline.Register(new StampDispatchBehavior(runDescriptor.ScenarioContext), "Stamps outgoing messages with session ID");
            builder.Pipeline.Register(new DiscardMessagesBehavior(runDescriptor.ScenarioContext), "Discards messages based on session ID");

            builder.SendFailedMessagesTo("error");

            builder.TypesToIncludeInScan(typesToInclude);
            builder.EnableInstallers();
            
            await builder.DefineTransport(runDescriptor, endpointConfiguration).ConfigureAwait(false);
            builder.RegisterComponentsAndInheritanceHierarchy(runDescriptor);
            await builder.DefinePersistence(runDescriptor, endpointConfiguration).ConfigureAwait(false);

            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic).SetValue(runDescriptor.ScenarioContext, endpointConfiguration.EndpointName);

            builder.DisableFeature<TimeoutManager>();

            var recoverability = builder.Recoverability();
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
           
            configurationBuilderCustomization(builder);

            return builder;
        }

        List<Type> typesToInclude;
    }
}