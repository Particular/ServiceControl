namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, DataStoreConfiguration dataStoreConfiguration, Action<EndpointConfiguration> customEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings)
        {
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customServiceControlSettings = customServiceControlSettings;
            this.customEndpointConfiguration = customEndpointConfiguration;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            transportIntegration = transportToUse;
            this.dataStoreConfiguration = dataStoreConfiguration;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Dictionary<string, dynamic> SettingsPerInstance => runner.SettingsPerInstance;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, dataStoreConfiguration, customEndpointConfiguration, customAuditEndpointConfiguration, customServiceControlSettings, customServiceControlAuditSettings);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        DataStoreConfiguration dataStoreConfiguration;
        Action<EndpointConfiguration> customEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        ServiceControlComponentRunner runner;
        Action<Settings> customServiceControlSettings;
        Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
    }
}