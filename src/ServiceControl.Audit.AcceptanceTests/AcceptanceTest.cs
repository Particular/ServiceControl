namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Dapper;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
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
        public JsonSerializerSettings SerializerSettings => serviceControlRunnerBehavior.SerializerSettings;
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

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(TransportIntegration, DataStoreConfiguration, s => SetSettings(s), s => CustomConfiguration(s));
            TestContext.WriteLine($"Using transport {TransportIntegration.Name}");
            TestContext.WriteLine($"Using data store {DataStoreConfiguration.DataStoreTypeName}");

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
        protected ITransportIntegration TransportIntegration;
        protected DataStoreConfiguration DataStoreConfiguration;

        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }
}