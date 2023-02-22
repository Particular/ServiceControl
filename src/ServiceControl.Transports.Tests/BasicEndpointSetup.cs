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
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var typesToInclude = endpointConfiguration.GetTypesScopedByTestClass().ToList();

            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.TypesToIncludeInScan(typesToInclude);
            builder.EnableInstallers();

            builder.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            configurationBuilderCustomization(builder);


            return Task.FromResult(builder);
        }


    }
}