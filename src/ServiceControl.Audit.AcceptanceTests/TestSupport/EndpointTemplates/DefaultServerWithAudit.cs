namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Audit.Infrastructure;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Action<EndpointConfiguration> configurationBuilderCustomization)
        {
            return new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, builder =>
            {
                builder.AuditProcessedMessagesTo("audit");

                configurationBuilderCustomization(builder);
            });
        }
    }
}