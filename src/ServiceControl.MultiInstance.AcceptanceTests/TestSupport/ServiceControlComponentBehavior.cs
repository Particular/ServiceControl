namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentBehavior : IComponentBehavior, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentBehavior(ITransportIntegration transportToUse, Action<EndpointConfiguration> customEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<ServiceControl.Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings, Action<IHostBuilder> customizeHostBuilder, Action<IHostBuilder> customizeAuditHostBuilder)
        {
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customizeHostBuilder = customizeHostBuilder;
            this.customizeAuditHostBuilder = customizeAuditHostBuilder;
            this.customServiceControlSettings = customServiceControlSettings;
            this.customEndpointConfiguration = customEndpointConfiguration;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            transportIntegration = transportToUse;
        }

        public Dictionary<string, HttpClient> HttpClients => runner.HttpClients;
        public JsonSerializerSettings SerializerSettings => runner.SerializerSettings;
        public Dictionary<string, dynamic> SettingsPerInstance => runner.SettingsPerInstance;

        public async Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            runner = new ServiceControlComponentRunner(transportIntegration, customEndpointConfiguration, customAuditEndpointConfiguration, customServiceControlSettings, customServiceControlAuditSettings, customizeHostBuilder, customizeAuditHostBuilder);
            await runner.Initialize(run).ConfigureAwait(false);
            return runner;
        }

        ITransportIntegration transportIntegration;
        Action<EndpointConfiguration> customEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        ServiceControlComponentRunner runner;
        Action<Settings> customServiceControlSettings;
        Action<ServiceControl.Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
        Action<IHostBuilder> customizeHostBuilder;
        Action<IHostBuilder> customizeAuditHostBuilder;
    }
}