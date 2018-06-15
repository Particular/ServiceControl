namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;

    public class DefaultServerWithoutAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerWithAudit().GetConfiguration(runDescriptor, endpointConfiguration, b =>
            {
                b.DisableFeature<Audit>();
                configurationBuilderCustomization(b);
            });
        }
    }
}