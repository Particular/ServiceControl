namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        private ITransportIntegration transportIntegration;
        private Action<string, Settings> setInstanceSettings;
        private Action<Settings> setSettings;
        private Action<EndpointConfiguration> customConfiguration;
        private Action<string, EndpointConfiguration> customInstanceConfiguration;
        private string[] instanceNames;
        private ServiceControlComponentRunner runner;


        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<string, Settings> setInstanceSettings, Action<EndpointConfiguration> customConfiguration, Action<string, EndpointConfiguration> customInstanceConfiguration)
        {
            this.customInstanceConfiguration = customInstanceConfiguration;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
            this.setInstanceSettings = setInstanceSettings;
            transportIntegration = transportToUse;
        }

        public void Initialize(string[] instanceNames)
        {
            this.instanceNames = instanceNames;
        }

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(instanceNames, transportIntegration, setSettings, setInstanceSettings, customConfiguration, customInstanceConfiguration);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Dictionary<string, Settings> SettingsPerInstance => runner.SettingsPerInstance;
        public Dictionary<string, OwinHttpMessageHandler> Handlers  => runner.Handlers;
    }
}