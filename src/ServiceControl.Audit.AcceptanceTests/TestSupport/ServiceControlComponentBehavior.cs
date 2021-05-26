namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration)
        {
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public string Port => runner.Port;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, setSettings, customConfiguration);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        ServiceControlComponentRunner runner;
    }
}