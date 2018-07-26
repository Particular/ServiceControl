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
        public ServiceControlComponentBehavior(string[] instanceNames, ITransportIntegration transportToUse, Action<Settings> setSettings, Action<string, Settings> setInstanceSettings, Action<EndpointConfiguration> customConfiguration, Action<string, EndpointConfiguration> customInstanceConfiguration)
        {
            this.instanceNames = instanceNames;
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
            if (runner != null)
            {
                return runner;
            }

            runner = new ServiceControlComponentRunner(instanceNames, transportIntegration, setSettings, setInstanceSettings, customConfiguration, customInstanceConfiguration);
            await runner.Initialize().ConfigureAwait(false);
            return runner;
        }

        public async Task Stop()
        {
            if (runner != null)
            {
                await runner.StopInternal().ConfigureAwait(false);
                runner = null;
            }
        }

        readonly ITransportIntegration transportIntegration;
        readonly Action<string, Settings> setInstanceSettings;
        readonly Action<Settings> setSettings;
        readonly Action<EndpointConfiguration> customConfiguration;
        readonly Action<string, EndpointConfiguration> customInstanceConfiguration;
        readonly string[] instanceNames;
        ServiceControlComponentRunner runner;
    }
}