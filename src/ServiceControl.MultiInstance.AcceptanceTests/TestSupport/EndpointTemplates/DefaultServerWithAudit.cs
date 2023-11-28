namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using Particular.ServiceControl;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, async builder =>
            {
                builder.AuditProcessedMessagesTo("audit");

                await configurationBuilderCustomization(builder);
            });
    }
}