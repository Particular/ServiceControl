namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.AcceptanceTests;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceBus.Management.Infrastructure.Settings;
    using EndpointConfiguration = NServiceBus.EndpointConfiguration;
    using AuditInstanceTestsSupport = ServiceControl.Audit.AcceptanceTests.TestSupport;
    using PrimaryInstanceTestsSupport = ServiceControl.AcceptanceTests.TestSupport;
    using PrimaryInstanceSettings = ServiceBus.Management.Infrastructure.Settings.Settings;
    using AuditInstanceSettings = ServiceControl.Audit.Infrastructure.Settings.Settings;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, Action<EndpointConfiguration> customPrimaryEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings)
        {
            this.customServiceControlSettings = customServiceControlSettings;
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            this.customPrimaryEndpointConfiguration = customPrimaryEndpointConfiguration;
            this.transportToUse = transportToUse;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Dictionary<string, HttpClient> HttpClients { get; } = [];
        public Dictionary<string, JsonSerializerOptions> SerializerOptions { get; } = [];
        public Dictionary<string, dynamic> SettingsPerInstance { get; } = [];

        public async Task Initialize(RunDescriptor run)
        {
            SettingsPerInstance.Clear();

            auditInstanceComponentRunner = new AuditInstanceTestsSupport.ServiceControlComponentRunner(
                transportToUse,
                new AcceptanceTestStorageConfiguration(), auditSettings =>
                {
                    auditSettings.ServiceControlQueueAddress = PrimaryInstanceSettings.DEFAULT_SERVICE_NAME;
                    customServiceControlAuditSettings(auditSettings);
                    SettingsPerInstance[AuditInstanceSettings.DEFAULT_SERVICE_NAME] = auditSettings;
                }, auditEndpointConfiguration =>
                {
                    var scanner = auditEndpointConfiguration.AssemblyScanner();
                    // TODO Maybe we need to find a more robust way to do this. For example excluding assemblies we find by convention
                    var excludedAssemblies = new[]
                    {
                        "ServiceControl.Persistence.RavenDB.dll",
                        "ServiceControl.AcceptanceTests.RavenDB.dll",
                        "ServiceControl.Audit.AcceptanceTests.dll",
                        Path.GetFileName(typeof(PrimaryInstanceSettings).Assembly.Location),
                        Path.GetFileName(typeof(ServiceControlComponentRunner).Assembly.Location),
                    };
                    scanner.ExcludeAssemblies(excludedAssemblies);

                    customAuditEndpointConfiguration(auditEndpointConfiguration);
                },
                _ => { },
                _ => { });
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(run.ScenarioContext, AuditInstanceSettings.DEFAULT_SERVICE_NAME);
            await auditInstanceComponentRunner.Initialize(run);

            HttpClients[AuditInstanceSettings.DEFAULT_SERVICE_NAME] = auditInstanceComponentRunner.HttpClient;
            SerializerOptions[AuditInstanceSettings.DEFAULT_SERVICE_NAME] = auditInstanceComponentRunner.SerializerOptions;

            var remoteInstances = new[] { new RemoteInstanceSetting($"http://localhost:44444/api") };
            primaryInstanceComponentRunner = new PrimaryInstanceTestsSupport.ServiceControlComponentRunner(
                transportToUse,
                new ServiceControl.AcceptanceTests.AcceptanceTestStorageConfiguration(), primarySettings =>
                {
                    primarySettings.RemoteInstances = remoteInstances;
                    customServiceControlSettings(primarySettings);
                    SettingsPerInstance[PrimaryInstanceSettings.DEFAULT_SERVICE_NAME] = primarySettings;
                },
                primaryEndpointConfiguration =>
                {
                    var scanner = primaryEndpointConfiguration.AssemblyScanner();
                    // TODO Maybe we need to find a more robust way to do this. For example excluding assemblies we find by convention
                    var excludedAssemblies = new[]
                    {
                        "ServiceControl.Persistence.RavenDB.dll",
                        "ServiceControl.AcceptanceTests.RavenDB.dll",
                        "ServiceControl.Audit.AcceptanceTests.dll",
                        Path.GetFileName(typeof(AuditInstanceSettings).Assembly.Location),
                        typeof(ServiceControlComponentRunner).Assembly.GetName().Name
                    };
                    scanner.ExcludeAssemblies(excludedAssemblies);

                    customPrimaryEndpointConfiguration(primaryEndpointConfiguration);
                },
                primaryHostBuilder =>
                {
                    // While this code looks generic, it won't support adding more than one remote instance 
                    primaryHostBuilder.Services.AddSingleton(_ => new HttpMessageInvoker(auditInstanceComponentRunner.InstanceTestServer.CreateHandler()));
                    foreach (var remoteInstance in remoteInstances)
                    {
                        var remoteInstanceHttpClientBuilder = primaryHostBuilder.Services.AddHttpClient(remoteInstance.InstanceId);
                        remoteInstanceHttpClientBuilder.ConfigurePrimaryHttpMessageHandler(_ => auditInstanceComponentRunner.InstanceTestServer.CreateHandler());
                    }
                });
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(run.ScenarioContext, PrimaryInstanceSettings.DEFAULT_SERVICE_NAME);
            await primaryInstanceComponentRunner.Initialize(run);

            HttpClients[PrimaryInstanceSettings.DEFAULT_SERVICE_NAME] = primaryInstanceComponentRunner.HttpClient;
            SerializerOptions[PrimaryInstanceSettings.DEFAULT_SERVICE_NAME] = primaryInstanceComponentRunner.SerializerOptions;
        }

        public override async Task Stop()
        {
            await auditInstanceComponentRunner.Stop();
            await primaryInstanceComponentRunner.Stop();
        }
        ITransportIntegration transportToUse;
        Action<EndpointConfiguration> customPrimaryEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
        Action<Settings> customServiceControlSettings;
        Audit.AcceptanceTests.TestSupport.ServiceControlComponentRunner auditInstanceComponentRunner;
        PrimaryInstanceTestsSupport.ServiceControlComponentRunner primaryInstanceComponentRunner;
    }
}