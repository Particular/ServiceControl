namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;
    using ServiceControl.Audit.Infrastructure.Settings;
    using TestSupport;

    [TestFixture]
    abstract class AcceptanceTest : NServiceBusAcceptanceTest, IAcceptanceTestInfrastructureProvider
    {
        protected AcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

        public HttpClient HttpClient => serviceControlRunnerBehavior.HttpClient;
        public JsonSerializerOptions SerializerOptions => serviceControlRunnerBehavior.SerializerOptions;
        public string Port => serviceControlRunnerBehavior.Port;

        // TODO Check why this is necessary and if it can be removed
        protected IServiceProvider ServiceProvider => serviceControlRunnerBehavior.ServiceProvider;

        [OneTimeSetUp]
        public static void OneTimeSetup() => Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);

        [SetUp]
        public async Task Setup()
        {
            SetSettings = _ => { };
            CustomConfiguration = _ => { };

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

            StorageConfiguration = new AcceptanceTestStorageConfiguration();

            await StorageConfiguration.Configure();

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(TransportIntegration, StorageConfiguration, s => SetSettings(s), s => CustomConfiguration(s), d => SetStorageConfiguration(d));
            TestContext.WriteLine($"Using persistence {StorageConfiguration.PersistenceType}");
        }

        [TearDown]
        public Task Teardown()
        {
            TransportIntegration = null;
            Trace.Flush();
            Trace.Close();
            Trace.Listeners.Remove(textWriterTraceListener);

            return StorageConfiguration.Cleanup();
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>() where T : ScenarioContext, new() => Define<T>(c => { });

        protected IScenarioWithEndpointBehavior<T> Define<T>(Action<T> contextInitializer) where T : ScenarioContext, new() =>
            Scenario.Define(contextInitializer)
                .WithComponent(serviceControlRunnerBehavior);

        protected Action<EndpointConfiguration> CustomConfiguration = _ => { };
        protected Action<Settings> SetSettings = _ => { };
        protected Action<IDictionary<string, string>> SetStorageConfiguration = _ => { };
        protected ITransportIntegration TransportIntegration;
        protected AcceptanceTestStorageConfiguration StorageConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}