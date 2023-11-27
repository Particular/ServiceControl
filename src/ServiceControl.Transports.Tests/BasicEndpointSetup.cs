namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;

    public class BasicEndpointSetup : IEndpointSetupTemplate
    {
        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor,
            EndpointCustomizationConfiguration endpointCustomization,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var endpointConfiguration = new EndpointConfiguration(endpointCustomization.EndpointName);

            // we don't use installers
            //endpointConfiguration.EnableInstallers();
            endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();
            endpointConfiguration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await configurationBuilderCustomization(endpointConfiguration);

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            endpointConfiguration.ScanTypesForTest(endpointCustomization);

            return endpointConfiguration;
        }
    }
}