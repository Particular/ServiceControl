namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Microsoft.Extensions.Hosting;
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
        public HttpClient HttpClient => serviceControlRunnerBehavior.HttpClient;
        public JsonSerializerOptions SerializerOptions => serviceControlRunnerBehavior.SerializerOptions;
        protected IServiceProvider ServiceProvider => serviceControlRunnerBehavior.ServiceProvider;

        [SetUp]
        public void Setup()
        {
            SetSettings = _ => { };
            CustomConfiguration = _ => { };
            CustomizeHostBuilder = _ => { };

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

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(TransportIntegration, StorageConfiguration, s => SetSettings(s), s => CustomConfiguration(s), d => SetStorageConfiguration(d), hb => CustomizeHostBuilder(hb));
            TestContext.Out.WriteLine($"Using persistence {StorageConfiguration.PersistenceType}");
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
        protected Action<IHostApplicationBuilder> CustomizeHostBuilder = _ => { };
        protected ITransportIntegration TransportIntegration;
        protected AcceptanceTestStorageConfiguration StorageConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}