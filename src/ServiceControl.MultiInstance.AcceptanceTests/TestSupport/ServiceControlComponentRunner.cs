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
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
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
        public ServiceControlComponentRunner(ITransportIntegration transportToUse,
            Action<EndpointConfiguration> customPrimaryEndpointConfiguration,
            Action<EndpointConfiguration> customAuditEndpointConfiguration,
            Action<Settings> customServiceControlSettings,
            Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings,
            Action<IHostApplicationBuilder> primaryHostBuilderCustomization,
            Action<IHostApplicationBuilder> auditHostBuilderCustomization)
        {
            this.customServiceControlSettings = customServiceControlSettings;
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            this.customPrimaryEndpointConfiguration = customPrimaryEndpointConfiguration;
            this.transportToUse = transportToUse;
            this.primaryHostBuilderCustomization = primaryHostBuilderCustomization;
            this.auditHostBuilderCustomization = auditHostBuilderCustomization;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Dictionary<string, HttpClient> HttpClients { get; } = [];
        public Dictionary<string, JsonSerializerOptions> SerializerOptions { get; } = [];
        public Dictionary<string, dynamic> SettingsPerInstance { get; } = [];

        public Dictionary<string, TestServer> TestServerPerRemoteInstance { get; } = [];

        public async Task Initialize(RunDescriptor run)
        {
            SettingsPerInstance.Clear();

            // The way we are setting up things here means we assume there is only one remote instance. Should we move away from this approach
            // parts of the logic in this class would have to be augmented to dynamically spin up multiple audit instances based on some configuration
            // currently we don't need that so YAGNI.
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
                auditHostBuilder => auditHostBuilderCustomization(auditHostBuilder));
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(run.ScenarioContext, AuditInstanceSettings.DEFAULT_SERVICE_NAME);
            await auditInstanceComponentRunner.Initialize(run);

            HttpClients[AuditInstanceSettings.DEFAULT_SERVICE_NAME] = auditInstanceComponentRunner.HttpClient;
            SerializerOptions[AuditInstanceSettings.DEFAULT_SERVICE_NAME] = auditInstanceComponentRunner.SerializerOptions;
            var auditInstance = new RemoteInstanceSetting(auditInstanceComponentRunner.InstanceTestServer.BaseAddress.ToString());
            TestServerPerRemoteInstance[auditInstance.InstanceId] = auditInstanceComponentRunner.InstanceTestServer;

            primaryInstanceComponentRunner = new PrimaryInstanceTestsSupport.ServiceControlComponentRunner(
                transportToUse,
                new ServiceControl.AcceptanceTests.AcceptanceTestStorageConfiguration(), primarySettings =>
                {
                    primarySettings.RemoteInstances = [auditInstance];
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
                    // The http message invoker is a singleton and can invoke any arbitrary destination. Hard wiring things to the audit instance
                    // is not ideal. Once we are introducing more instances than just the audit and the primary this code would have to change.
                    // For example one way to deal with this is to have a custom invoker that figures out the right target based on the base address
                    // in the request URI.
                    primaryHostBuilder.Services.AddKeyedSingleton("Forwarding", () => auditInstanceComponentRunner.InstanceTestServer.CreateHandler());
                    foreach (var remoteInstance in ((PrimaryInstanceSettings)SettingsPerInstance[PrimaryInstanceSettings.DEFAULT_SERVICE_NAME]).RemoteInstances)
                    {
                        if (TestServerPerRemoteInstance.TryGetValue(remoteInstance.InstanceId, out var testServer))
                        {
                            primaryHostBuilder.Services.AddKeyedSingleton(remoteInstance.InstanceId, () => testServer.CreateHandler());
                        }
                        else
                        {
                            primaryHostBuilder.Services.AddKeyedSingleton<Func<HttpMessageHandler>>(remoteInstance.InstanceId, () => throw new InvalidOperationException($"There is no registered HttpMessageHandler factory for instance {remoteInstance.BaseAddress}."));
                        }
                    }

                    primaryHostBuilderCustomization(primaryHostBuilder);
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
        Action<IHostApplicationBuilder> primaryHostBuilderCustomization;
        Action<IHostApplicationBuilder> auditHostBuilderCustomization;
        Action<Settings> customServiceControlSettings;
        Audit.AcceptanceTests.TestSupport.ServiceControlComponentRunner auditInstanceComponentRunner;
        PrimaryInstanceTestsSupport.ServiceControlComponentRunner primaryInstanceComponentRunner;
    }
}