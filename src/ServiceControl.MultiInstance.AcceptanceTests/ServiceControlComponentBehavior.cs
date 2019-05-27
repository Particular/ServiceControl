namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<string, Settings> setInstanceSettings, Action<EndpointConfiguration> customConfiguration, Action<string, EndpointConfiguration> customInstanceConfiguration)
        {
            this.customInstanceConfiguration = customInstanceConfiguration;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
            this.setInstanceSettings = setInstanceSettings;
            transportIntegration = transportToUse;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Dictionary<string, Settings> SettingsPerInstance => runner.SettingsPerInstance;
        public Dictionary<string, OwinHttpMessageHandler> Handlers => runner.Handlers;
        public Dictionary<string, BusInstance> Busses => runner.Busses;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(instanceNames, transportIntegration, setSettings, setInstanceSettings, customConfiguration, customInstanceConfiguration);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        public void Initialize(string[] instanceNames)
        {
            this.instanceNames = instanceNames;
        }

        ITransportIntegration transportIntegration;
        Action<string, Settings> setInstanceSettings;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<string, EndpointConfiguration> customInstanceConfiguration;
        string[] instanceNames;
        ServiceControlComponentRunner runner;
    }
}