namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting.InfrastructureConfig;
    using TestSupport;

    [TestFixture]
    abstract class AcceptanceTest : NServiceBusAcceptanceTest, IAcceptanceTestInfrastructureProvider
    {
        public IDomainEvents DomainEvents => serviceControlRunnerBehavior.DomainEvents;
        public HttpClient HttpClient => serviceControlRunnerBehavior.HttpClient;
        public JsonSerializerOptions SerializerOptions => serviceControlRunnerBehavior.SerializerOptions;
        public Settings Settings => serviceControlRunnerBehavior.Settings;
        public Func<HttpMessageHandler> HttpMessageHandlerFactory => serviceControlRunnerBehavior.HttpMessageHandlerFactory;

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

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(
                TransportIntegration,
                StorageConfiguration,
                s => SetSettings(s),
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
        protected void ExecuteWhen(Func<bool> execute, Func<IDomainEvents, Task> action, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME)
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

        protected IScenarioWithEndpointBehavior<T> Define<T>() where T : ScenarioContext, new() => Define<T>(c => { });

        protected IScenarioWithEndpointBehavior<T> Define<T>(Action<T> contextInitializer) where T : ScenarioContext, new() =>
            Scenario.Define(contextInitializer)
                .WithComponent(serviceControlRunnerBehavior);

        protected Action<EndpointConfiguration> CustomConfiguration = _ => { };
        protected Action<Settings> SetSettings = _ => { };
        protected Action<IHostApplicationBuilder> CustomizeHostBuilder = _ => { };
        protected ITransportIntegration TransportIntegration;
        protected AcceptanceTestStorageConfiguration StorageConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}