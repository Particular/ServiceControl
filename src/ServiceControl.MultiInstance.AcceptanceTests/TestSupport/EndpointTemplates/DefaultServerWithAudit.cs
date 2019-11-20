﻿namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using Particular.ServiceControl;

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