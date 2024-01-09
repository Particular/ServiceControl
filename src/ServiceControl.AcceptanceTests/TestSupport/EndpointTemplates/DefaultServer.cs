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
        // TODO: Revisit the default server base having a bootstrapper reference
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServerBase<SetupBootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, async b =>
            {
                b.DisableFeature<Audit>();

                await configurationBuilderCustomization(b);
            });
    }
}