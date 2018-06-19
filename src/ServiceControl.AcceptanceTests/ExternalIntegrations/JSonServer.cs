namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;

    public class JsonServer : IEndpointSetupTemplate
    {

        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerWithAudit().GetConfiguration(runDescriptor, endpointConfiguration, b =>
            {
                b.UseSerialization<NewtonsoftSerializer>();
                configurationBuilderCustomization(b);
            });
        }
    }
}