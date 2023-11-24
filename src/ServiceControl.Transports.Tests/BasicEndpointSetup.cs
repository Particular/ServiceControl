namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Linq;
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
            var typesToInclude = endpointCustomization.GetTypesScopedByTestClass().ToList();

            var endpointConfiguration = new EndpointConfiguration(endpointCustomization.EndpointName);
            endpointConfiguration.TypesToIncludeInScan(typesToInclude);

            // we don't use installers
            //endpointConfiguration.EnableInstallers();
            endpointConfiguration.UseSerialization<NewtonsoftJsonSerializer>();
            endpointConfiguration.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            await configurationBuilderCustomization(endpointConfiguration);

            return endpointConfiguration;
        }
    }
}