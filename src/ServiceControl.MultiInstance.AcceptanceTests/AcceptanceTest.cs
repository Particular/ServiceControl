namespace ServiceControl.MultiInstance.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
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
        protected AcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

        protected static string ServiceControlInstanceName { get; } = Settings.DEFAULT_SERVICE_NAME;
        protected static string ServiceControlAuditInstanceName { get; } = Audit.Infrastructure.Settings.Settings.DEFAULT_SERVICE_NAME;

        public Dictionary<string, HttpClient> HttpClients => serviceControlRunnerBehavior.HttpClients;
        public Dictionary<string, JsonSerializerOptions> SerializerOptions => serviceControlRunnerBehavior.SerializerOptions;
        public Dictionary<string, dynamic> SettingsPerInstance => serviceControlRunnerBehavior.SettingsPerInstance;

        [OneTimeSetUp]
        public static void OneTimeSetup() => Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);

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