namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using Features;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Monitoring;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, b =>
            {
                b.DisableFeature<Audit>();

                // will work on all the cloud transports
                b.UseSerialization<NewtonsoftSerializer>();
                b.DisableFeature<AutoSubscribe>();
                b.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            
                b.RegisterComponents(r => { b.GetSettings().Set("SC.ConfigureComponent", r); });
                b.Pipeline.Register<TraceIncomingBehavior.Registration>();
                b.Pipeline.Register<TraceOutgoingBehavior.Registration>();

                b.GetSettings().Set("SC.ScenarioContext", runDescriptor.ScenarioContext);

                configurationBuilderCustomization(b);
            });
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts")
                                       && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
}
}