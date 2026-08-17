namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Monitoring;
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
        public JsonSerializerOptions SerializerOptions => runner.SerializerOptions;

#pragma warning disable PS0018 // IComponentBehavior declares this without a CancellationToken
        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
#pragma warning restore PS0018
        {
            runner = new ServiceControlComponentRunner(transportIntegration, setSettings, customConfiguration);
            await runner.Initialize(run);
            return runner;
        }

        ITransportIntegration transportIntegration;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        ServiceControlComponentRunner runner;
    }
}