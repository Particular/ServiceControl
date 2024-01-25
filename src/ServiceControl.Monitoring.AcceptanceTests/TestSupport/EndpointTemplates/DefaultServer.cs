namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;

    public class DefaultServer : IEndpointSetupTemplate
    {
        // TODO: Revisit the default server base having a bootstrapper reference
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServerBase<SetupBootstrapper>(new ConfigureEndpointLearningTransport()).GetConfiguration(runDescriptor, endpointConfiguration, async b =>
            {
                b.DisableFeature<Audit>();

                await configurationBuilderCustomization(b);
            });
    }
}