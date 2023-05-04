namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.Audit.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IDictionary<string, string>> setStorageConfiguration)
        {
            this.customConfiguration = customConfiguration;
            this.persistenceToUse = persistenceToUse;
            this.setSettings = setSettings;
            this.setStorageConfiguration = setStorageConfiguration;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public IServiceProvider ServiceProvider => runner.ServiceProvider;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public string Port => runner.Port;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, persistenceToUse, setSettings, customConfiguration, setStorageConfiguration);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        AcceptanceTestStorageConfiguration persistenceToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        ServiceControlComponentRunner runner;
        Action<IDictionary<string, string>> setStorageConfiguration;
    }
}