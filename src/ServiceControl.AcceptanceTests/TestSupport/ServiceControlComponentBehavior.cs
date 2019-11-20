namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

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
        public Settings Settings => runner.Settings;
        public OwinHttpMessageHandler Handler => runner.Handler;
        public BusInstance Bus => runner.Bus;
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