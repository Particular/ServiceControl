namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    public class JsonServer : IEndpointSetupTemplate
    {
        public BusConfiguration GetConfiguration(RunDescriptor runDescriptor, EndpointConfiguration endpointConfiguration, IConfigurationSource configSource, Action<BusConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServer().GetConfiguration(runDescriptor, endpointConfiguration, configSource, b =>
            {
                b.UseSerialization<JsonSerializer>();
                b.DisableFeature<AutoSubscribe>();
                configurationBuilderCustomization(b);
            });
        }
    }
}