namespace ServiceBus.Management.AcceptanceTests.Contexts
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;

    public class DefaultServerWithoutAudit : IEndpointSetupTemplate
    {
        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerWithAudit().GetConfiguration(runDescriptor, endpointConfiguration, configSource, b =>
            {
                b.DisableFeature<Audit>();
                configurationBuilderCustomization(b);
            });
        }
    }
}