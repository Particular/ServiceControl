namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using Features;
    using Particular.ServiceControl;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, b =>
            {
                b.DisableFeature<TimeoutManager>();

                var recoverability = b.Recoverability();
                recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
                recoverability.Immediate(immediate => immediate.NumberOfRetries(0));

                configurationBuilderCustomization(b);
            });
        }
    }
}