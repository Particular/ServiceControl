namespace ServiceControl.MultiInstance.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using AcceptanceTesting;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;
    using TestSupport;

    [TestFixture]
    abstract class AcceptanceTest : NServiceBusAcceptanceTest, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        protected static string ServiceControlInstanceName { get; } = PrimaryOptions.DEFAULT_INSTANCE_NAME;
        protected static string ServiceControlAuditInstanceName { get; } = Audit.Infrastructure.Settings.Settings.DEFAULT_INSTANCE_NAME;

        public Dictionary<string, HttpClient> HttpClients => serviceControlRunnerBehavior.HttpClients;
        public Dictionary<string, JsonSerializerOptions> SerializerOptions => serviceControlRunnerBehavior.SerializerOptions;
        public Dictionary<string, dynamic> SettingsPerInstance => serviceControlRunnerBehavior.SettingsPerInstance;

        [SetUp]
        public void Setup()
        {
            CustomPrimaryEndpointConfiguration = c => { };
            CustomAuditEndpointConfiguration = c => { };
            CustomServiceControlPrimarySettings = s => { };
            CustomServiceControlAuditSettings = s => { };

            var logfilesPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "logs");
            Directory.CreateDirectory(logfilesPath);
            var logFile = Path.Combine(logfilesPath, $"{TestContext.CurrentContext.Test.ID}-{TestContext.CurrentContext.Test.Name}.txt");
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            textWriterTraceListener = new TextWriterTraceListener(logFile);
            Trace.Listeners.Add(textWriterTraceListener);

            TransportIntegration = new ConfigureEndpointLearningTransport();

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(
                TransportIntegration,
                c => CustomPrimaryEndpointConfiguration(c),
                c => CustomAuditEndpointConfiguration(c),
                s => CustomServiceControlPrimarySettings(s),
                s => CustomServiceControlAuditSettings(s),
                b => PrimaryHostBuilderCustomization(b),
                b => AuditHostBuilderCustomization(b)
                );
        }

        [TearDown]
        public void Teardown()
        {
            TransportIntegration = null;
            Trace.Flush();
            Trace.Close();
            Trace.Listeners.Remove(textWriterTraceListener);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>() where T : ScenarioContext, new() => Define<T>(c => { });

        protected IScenarioWithEndpointBehavior<T> Define<T>(Action<T> contextInitializer) where T : ScenarioContext, new() =>
            Scenario.Define(contextInitializer)
                .WithComponent(serviceControlRunnerBehavior);

        protected Action<EndpointConfiguration> CustomPrimaryEndpointConfiguration = c => { };
        protected Action<EndpointConfiguration> CustomAuditEndpointConfiguration = c => { };
        protected Action<Settings> CustomServiceControlPrimarySettings = c => { };
        protected Action<Audit.Infrastructure.Settings.Settings> CustomServiceControlAuditSettings = c => { };
        protected Action<IHostApplicationBuilder> PrimaryHostBuilderCustomization = b => { };
        protected Action<IHostApplicationBuilder> AuditHostBuilderCustomization = b => { };
        protected ITransportIntegration TransportIntegration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}