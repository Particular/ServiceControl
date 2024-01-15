namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;
    using TestSupport;

    [TestFixture]
    //[Parallelizable(ParallelScope.All)]
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

        public IDomainEvents DomainEvents => serviceControlRunnerBehavior.DomainEvents;
        public HttpClient HttpClient => serviceControlRunnerBehavior.HttpClient;
        public JsonSerializerSettings SerializerSettings => serviceControlRunnerBehavior.SerializerSettings;
        public Settings Settings => serviceControlRunnerBehavior.Settings;
        public string Port => serviceControlRunnerBehavior.Port;
        public Func<HttpMessageHandler> HttpMessageHandlerFactory => serviceControlRunnerBehavior.HttpMessageHandlerFactory;

        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);
        }

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

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(
                TransportIntegration,
                StorageConfiguration,
                s =>
                {
                    SetSettings(s);
                },
                s => CustomConfiguration(s),
                hb => CustomizeHostBuilder(hb)
                );
        }

        [TearDown]
        public async Task Teardown()
        {
            TransportIntegration = null;
            Trace.Flush();
            Trace.Close();
            Trace.Listeners.Remove(textWriterTraceListener);

            await StorageConfiguration.Cleanup();
        }

#pragma warning disable IDE0060 // Remove unused parameter
        protected void ExecuteWhen(Func<bool> execute, Func<IDomainEvents, Task> action, string instanceName = Settings.DEFAULT_SERVICE_NAME)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var timeout = TimeSpan.FromSeconds(1);

            _ = Task.Run(async () =>
            {
                while (!SpinWait.SpinUntil(execute, timeout))
                {
                }

                await action(DomainEvents);
            });
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>() where T : ScenarioContext, new()
        {
            return Define<T>(c => { });
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(Action<T> contextInitializer) where T : ScenarioContext, new()
        {
            return Scenario.Define(contextInitializer)
                .WithComponent(serviceControlRunnerBehavior);
        }

        protected Action<EndpointConfiguration> CustomConfiguration = _ => { };
        protected Action<Settings> SetSettings = _ => { };
        protected Action<IHostApplicationBuilder> CustomizeHostBuilder = _ => { };
        protected ITransportIntegration TransportIntegration;
        protected AcceptanceTestStorageConfiguration StorageConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}