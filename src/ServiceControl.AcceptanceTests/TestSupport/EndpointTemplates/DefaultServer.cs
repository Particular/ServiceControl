namespace ServiceControl.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;
    using Particular.ServiceControl;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, b =>
            {
                b.DisableFeature<Audit>();

                configurationBuilderCustomization(b);
            });
        }
    }
}