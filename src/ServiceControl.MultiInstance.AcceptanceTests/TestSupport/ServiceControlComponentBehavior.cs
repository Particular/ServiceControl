namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, Action<EndpointConfiguration> customEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<ServiceControl.Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings)
        {
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customServiceControlSettings = customServiceControlSettings;
            this.customEndpointConfiguration = customEndpointConfiguration;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            transportIntegration = transportToUse;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Dictionary<string, dynamic> SettingsPerInstance => runner.SettingsPerInstance;
        public Dictionary<string, OwinHttpMessageHandler> Handlers => runner.Handlers;
        public Dictionary<string, dynamic> Busses => runner.Busses;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, customEndpointConfiguration, customAuditEndpointConfiguration, customServiceControlSettings, customServiceControlAuditSettings);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        Action<EndpointConfiguration> customEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        ServiceControlComponentRunner runner;
        Action<Settings> customServiceControlSettings;
        Action<ServiceControl.Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
    }
}