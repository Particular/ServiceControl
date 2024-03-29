namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse,
            Action<EndpointConfiguration> customPrimaryEndpointConfiguration,
            Action<EndpointConfiguration> customAuditEndpointConfiguration,
            Action<Settings> customServiceControlPrimarySettings,
            Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings,
            Action<IHostApplicationBuilder> primaryHostBuilderCustomization,
            Action<IHostApplicationBuilder> auditHostBuilderCustomization)
        {
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customServiceControlPrimarySettings = customServiceControlPrimarySettings;
            this.customPrimaryEndpointConfiguration = customPrimaryEndpointConfiguration;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            transportIntegration = transportToUse;
            this.primaryHostBuilderCustomization = primaryHostBuilderCustomization;
            this.auditHostBuilderCustomization = auditHostBuilderCustomization;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public Dictionary<string, JsonSerializerOptions> SerializerOptions => runner.SerializerOptions;
        public Dictionary<string, dynamic> SettingsPerInstance => runner.SettingsPerInstance;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration,
                customPrimaryEndpointConfiguration,
                customAuditEndpointConfiguration,
                customServiceControlPrimarySettings,
                customServiceControlAuditSettings,
                primaryHostBuilderCustomization,
                auditHostBuilderCustomization);
            await runner.Initialize(run);
            return runner;
        }

        ITransportIntegration transportIntegration;
        Action<EndpointConfiguration> customPrimaryEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        ServiceControlComponentRunner runner;
        Action<Settings> customServiceControlPrimarySettings;
        Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
        Action<IHostApplicationBuilder> primaryHostBuilderCustomization;
        Action<IHostApplicationBuilder> auditHostBuilderCustomization;
    }
}