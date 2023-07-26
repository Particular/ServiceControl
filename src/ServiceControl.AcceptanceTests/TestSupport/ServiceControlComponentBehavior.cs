namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Audit.AcceptanceTests;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostBuilder> hostBuilderCustomization, Action<IDictionary<string, string>> setStorageConfiguration)
        {
            this.customConfiguration = customConfiguration;
            this.persistenceToUse = persistenceToUse;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.setStorageConfiguration = setStorageConfiguration;
            this.setSettings = setSettings;
            transportIntegration = transportToUse;
        }

        public HttpClient HttpClient => runner.HttpClient;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Settings Settings => runner.Settings;
        public OwinHttpMessageHandler Handler => runner.Handler;
        public string Port => runner.Port;
        public IDomainEvents DomainEvents => runner.DomainEvents;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, persistenceToUse, setSettings, customConfiguration, hostBuilderCustomization, setStorageConfiguration);
            await runner.Initialize(run);
            return runner;
        }

        ITransportIntegration transportIntegration;
        AcceptanceTestStorageConfiguration persistenceToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<IHostBuilder> hostBuilderCustomization;
        ServiceControlComponentRunner runner;
        Action<IDictionary<string, string>> setStorageConfiguration;
    }
}