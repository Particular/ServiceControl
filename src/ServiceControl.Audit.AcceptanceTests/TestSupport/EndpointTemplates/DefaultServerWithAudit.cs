namespace ServiceControl.Audit.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    public class DefaultServerWithAudit : IEndpointSetupTemplate
    {
        public Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            //TODO generic type used to be Bootstrapper, so figure out what we need to change to get this working again
            new DefaultServerBase<string>().GetConfiguration(runDescriptor, endpointConfiguration, async builder =>
            {
                builder.AuditProcessedMessagesTo("audit");
                builder.EnableInstallers();

                await configurationBuilderCustomization(builder);
            });
    }
}