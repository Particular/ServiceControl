namespace ServiceControl.Audit.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            new DefaultServerBase<Bootstrapper>().GetConfiguration(runDescriptor, endpointConfiguration, async builder =>
            {
                builder.AuditProcessedMessagesTo("audit");
                builder.EnableInstallers();

                await configurationBuilderCustomization(builder);
            });
    }
}