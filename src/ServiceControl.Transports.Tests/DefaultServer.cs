namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Features;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            var typesToInclude = endpointConfiguration.GetTypesScopedByTestClass().ToList();

            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.TypesToIncludeInScan(typesToInclude);
            builder.EnableInstallers();

            builder.DisableFeature<TimeoutManager>();
            builder.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));
            builder.SendFailedMessagesTo("error");

            builder.RegisterComponentsAndInheritanceHierarchy(runDescriptor);

            configurationBuilderCustomization(builder);


            return Task.FromResult(builder);
        }


    }
}