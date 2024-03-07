namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServer().GetConfiguration(runDescriptor, endpointConfiguration, async builder =>
            {
                builder.AuditProcessedMessagesTo("audit");

                await configurationBuilderCustomization(builder);
            });
    }
}