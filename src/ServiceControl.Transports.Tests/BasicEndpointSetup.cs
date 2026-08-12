namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;

    public class BasicEndpointSetup : IEndpointSetupTemplate
    {
#pragma warning disable PS0018 // IEndpointSetupTemplate declares this without a CancellationToken
#pragma warning disable PS0013 // IEndpointSetupTemplate declares the callback as Func<EndpointConfiguration, Task>
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor,
            EndpointCustomizationConfiguration endpointCustomization,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var endpointConfiguration = new EndpointConfiguration(endpointCustomization.EndpointName);

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            var recoverability = endpointConfiguration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));

            await configurationBuilderCustomization(endpointConfiguration);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            endpointConfiguration.ScanTypesForTest(endpointCustomization);

            return endpointConfiguration;
        }
#pragma warning restore PS0013
#pragma warning restore PS0018
    }
}