namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using Infrastructure;
    using Infrastructure.Installers;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Raw;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using TestSupport;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Transports;

    [TestFixture]
    public class When_running_setup_command__integration_test
    {
        string _workingDirectory;
        string _dbPath;

        #region setup to run before each test
        [SetUp]
        public void Setup()
        {
            EnsureEventSourceDoesNotExists();

            _workingDirectory = CreateRandomTempDirectory();
            _dbPath = Path.Combine(_workingDirectory, Path.GetRandomFileName());
            TransportInitializationHasBeenInvoked = false;
        }

        static void EnsureEventSourceDoesNotExists()
        {
            try
            {
                if (EventLog.SourceExists(CreateEventSource.SourceName))
                {
                    EventLog.DeleteEventSource(CreateEventSource.SourceName);
                }
            }
            catch
            {
                // ignored
            }

            Assert.IsTrue(EventLog.SourceExists(CreateEventSource.SourceName) == false,
                $"Running this test requires elevated privileges. The test setup couldn't confirm windows event log source {CreateEventSource.SourceName} doesn't exists before starting the test execution");
        }

        static string CreateRandomTempDirectory() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        #endregion setup to run before each test

        [Test]
        public async Task VerifySetupCommandBehavior()
        {
            #region Arrange
            Directory.CreateDirectory(_workingDirectory);

            var settings = new Settings
            {
                TransportConnectionString = _workingDirectory,
                DbPath = _dbPath,
                TransportCustomizationType = typeof(FakeTransportSpyTransportCustomization).AssemblyQualifiedName,
                ServiceControlQueueAddress = Settings.DEFAULT_SERVICE_NAME
            };

            var excludedAssemblies = new[]
            {
                Path.GetFileName(typeof(Settings).Assembly.CodeBase),
                typeof(ServiceControlComponentRunner).Assembly.GetName().Name,
                typeof(IComponentBehavior).Assembly.GetName().Name
            };
            #endregion Arrange

            //Act
            var setupBootstrapper = new SetupBootstrapper(settings, excludeAssemblies: excludedAssemblies.ToArray());
            await setupBootstrapper.Run(WindowsIdentity.GetCurrent().Name);

            //Assert
            QueuesGotCreated();
            EventLogSourceGotCreated();
            DatabaseGotCreated();
        }

        void QueuesGotCreated() => Assert.IsTrue(TransportInitializationHasBeenInvoked, "Unable to verify that the queue creation logic was invoked");

        void EventLogSourceGotCreated() => Assert.IsTrue(EventLog.SourceExists(CreateEventSource.SourceName), $"Windows EventViewer event source '{CreateEventSource.SourceName}' was not found");

        void DatabaseGotCreated()
        {
            Assert.IsTrue(File.Exists($"{_dbPath}/IndexDefinitions/indexes.txt"), "Unable to find database.");
            var indexesFileContent = File.ReadAllLines($"{_dbPath}/IndexDefinitions/indexes.txt");

            Assert.IsTrue(indexesFileContent.Any(i => i.Contains("SagaDetailsIndex")), "Database does not contain expected index SagaDetailsIndex");
            Assert.IsTrue(indexesFileContent.Any(i => i.Contains("MessagesViewIndex")), "Database does not contain expected index MessagesViewIndex");

            //IndexDefinitions/indexes.txt expected content. Consider verifying the whole file instead although the content could be in a different order.
            /*
             * 2 - ExpiryProcessedMessageIndex
                1 - ExpiryKnownEndpointsIndex
                5 - ExpirySagaAuditIndex
                6 - SagaDetailsIndex
                4 - MessagesViewIndex
                3 - FailedAuditImportIndex
             */
        }

        public static bool TransportInitializationHasBeenInvoked = false;

        #region FakeTransport
        public class LearningTransportSpy : LearningTransport
        {
            public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
            {
                TransportInitializationHasBeenInvoked = true;
                return base.Initialize(settings, connectionString);
            }
        }

        public class FakeTransportSpyTransportCustomization : TransportCustomization
        {
            public override void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeRawSendOnlyEndpoint(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeForErrorIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeForAuditIngestion(RawEndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeForMonitoringIngestion(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings) => CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override void CustomizeForReturnToSenderIngestion(RawEndpointConfiguration endpointConfiguration,
                TransportSettings transportSettings) =>
                CustomizeEndpoint(endpointConfiguration, transportSettings);

            public override IProvideQueueLength CreateQueueLengthProvider() => throw new NotImplementedException();

            static void CustomizeEndpoint(EndpointConfiguration endpointConfig, TransportSettings transportSettings)
            {
                var transport = endpointConfig.UseTransport<LearningTransportSpy>();
                transport.ConnectionString(transportSettings.ConnectionString);
            }

            static void CustomizeEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
            {

                var transport = endpointConfig.UseTransport<LearningTransportSpy>();
                transport.ConnectionString(transportSettings.ConnectionString);
            }
        }
        #endregion FakeTransport

        #region TearDown
        [TearDown]
        public void TearDown() => DeleteFolder(_workingDirectory);

        static void DeleteFolder(string path)
        {
            DirectoryInfo emptyTempDirectory = null;

            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                emptyTempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
                emptyTempDirectory.Create();
                var arguments = $"\"{emptyTempDirectory.FullName}\" \"{path.TrimEnd('\\')}\" /W:1  /R:1 /FFT /MIR /NFL";
                using (var process = Process.Start(new ProcessStartInfo("robocopy")
                {
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                }))
                {
                    process?.WaitForExit();
                }

                using (var windowsIdentity = WindowsIdentity.GetCurrent())
                {
                    var directorySecurity = new DirectorySecurity();
                    directorySecurity.SetOwner(windowsIdentity.User);
                    Directory.SetAccessControl(path, directorySecurity);
                }

                if (!(Directory.GetFiles(path).Any() || Directory.GetDirectories(path).Any()))
                {
                    Directory.Delete(path);
                }
            }
            finally
            {
                emptyTempDirectory?.Delete();
            }
        }
        #endregion TearDown
    }
}
