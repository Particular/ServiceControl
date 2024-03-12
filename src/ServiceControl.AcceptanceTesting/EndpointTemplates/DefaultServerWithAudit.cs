namespace ServiceControl.AcceptanceTesting.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public class DefaultServerWithAudit : DefaultServerBase
    {
        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointConfiguration, async builder =>
            {
                builder.AuditProcessedMessagesTo("audit");

                await configurationBuilderCustomization(builder);
            });
    }
}