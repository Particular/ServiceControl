namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Dapper;
    using Infrastructure.DomainEvents;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
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

        public IDomainEvents DomainEvents => serviceControlRunnerBehavior.DomainEvents;
        public HttpClient HttpClient => serviceControlRunnerBehavior.HttpClient;
        public JsonSerializerSettings SerializerSettings => serviceControlRunnerBehavior.SerializerSettings;
        public Settings Settings => serviceControlRunnerBehavior.Settings;
        public OwinHttpMessageHandler Handler => serviceControlRunnerBehavior.Handler;
        public string Port => serviceControlRunnerBehavior.Port;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);
        }

        [SetUp]
        public async Task Setup()
        {
            SetSettings = _ => { };
            CustomConfiguration = _ => { };

#if !NETCOREAPP2_0
            ConfigurationManager.GetSection("X");
#endif

            var logfilesPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "logs");
            Directory.CreateDirectory(logfilesPath);
            var logFile = Path.Combine(logfilesPath, $"{TestContext.CurrentContext.Test.ID}-{TestContext.CurrentContext.Test.Name}.txt");
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            textWriterTraceListener = new TextWriterTraceListener(logFile);
            Trace.Listeners.Add(textWriterTraceListener);

            TransportIntegration = (ITransportIntegration)TestSuiteConstraints.Current.CreateTransportConfiguration();

            DataStoreConfiguration = TestSuiteConstraints.Current.CreateDataStoreConfiguration();

            if (DataStoreConfiguration.DataStoreTypeName == nameof(DataStoreType.SqlDb))
            {
                await ResetSqlDb(DataStoreConfiguration.ConnectionString).ConfigureAwait(false);
            }

            var shouldBeRunOnAllTransports = GetType().GetCustomAttributes(typeof(RunOnAllTransportsAttribute), true).Any();
            if (!shouldBeRunOnAllTransports && TransportIntegration.Name != "Learning")
            {
                Assert.Inconclusive($"Not flagged with [RunOnAllTransports] therefore skipping this test with '{TransportIntegration.Name}'");
            }

            var shouldBeRunOnAllDataStores = GetType().GetCustomAttributes(typeof(RunOnAllDataStoresAttribute), true).Any();
            if (!shouldBeRunOnAllDataStores && DataStoreConfiguration.DataStoreTypeName != nameof(DataStoreType.RavenDb))
            {
                Assert.Inconclusive($"Not flagged with [RunOnAllDataStores] therefore skipping this test with '{DataStoreConfiguration.DataStoreTypeName}'");
            }

            TestContext.WriteLine($"Using transport {TransportIntegration.Name}");
            TestContext.WriteLine($"Using data store {DataStoreConfiguration.DataStoreTypeName}");
            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(TransportIntegration, DataStoreConfiguration, s => SetSettings(s), s => CustomConfiguration(s), hb => CustomizeHostBuilder(hb));

            RemoveOtherTransportAssemblies(TransportIntegration.TypeName);
        }

        [TearDown]
        public void Teardown()
        {
            TransportIntegration = null;
            Trace.Flush();
            Trace.Close();
            Trace.Listeners.Remove(textWriterTraceListener);
        }

        async Task ResetSqlDb(string connectionString)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var dropConstraints = "EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT all'";
                var dropTables = "EXEC sp_msforeachtable 'DROP TABLE ?'";

                await connection.OpenAsync().ConfigureAwait(false);
                await connection.ExecuteAsync(dropConstraints).ConfigureAwait(false);
                await connection.ExecuteAsync(dropTables).ConfigureAwait(false);
            }
        }

        static void RemoveOtherTransportAssemblies(string name)
        {
            var assembly = Type.GetType(name, true).Assembly;

            var currentDirectoryOfSelectedTransport = Path.GetDirectoryName(assembly.Location);
            var otherAssemblies = Directory.EnumerateFiles(currentDirectoryOfSelectedTransport, "ServiceControl.Transports.*.dll")
                .Where(transportAssembly => transportAssembly != assembly.Location);

            foreach (var transportAssembly in otherAssemblies)
            {
                File.Delete(transportAssembly);
            }
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
        protected Action<IHostBuilder> CustomizeHostBuilder = _ => { };
        protected ITransportIntegration TransportIntegration;
        protected DataStoreConfiguration DataStoreConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}