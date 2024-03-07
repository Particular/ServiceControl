namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;

    public class DefaultServerWithoutAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServer().GetConfiguration(runDescriptor, endpointConfiguration, async b =>
            {
                b.DisableFeature<Audit>();

                await configurationBuilderCustomization(b);
            });
    }
}