namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Raw;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Infrastructure.Installers;
    using Infrastructure.Settings;
    using Transports;

    /// <summary>
    /// <see href="https://michaelfeathers.silvrback.com/characterization-testing">Characterization</see> tests for setup command.
    /// Once there is more coverage from the acceptance testing angle this test can be removed, in the meantime this test mission is to help while attempting refactorings to the setup command. 
    /// </summary>
    [TestFixture]
    public class SetupCommandCharacterizationTests
    {
        string _workingDirectory;
        string _dbPath;

        [SetUp]
        public void Setup()
        {
            _workingDirectory = CreateRandomTempDirectory();
            _dbPath = Path.Combine(_workingDirectory, Path.GetRandomFileName());
        }

        static string CreateRandomTempDirectory() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        public const string TransportInitializedSpyFileName = "initialized.txt";

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
            #endregion Arrange

            //Act
            var setupBootstrapper = new SetupBootstrapper(settings);
            await setupBootstrapper.Run(null);

            //Assert
            QueuesGotCreated();
            EventLogSourceGotCreated();
            DatabaseGotCreated();
        }

        void QueuesGotCreated() => Assert.IsTrue(File.Exists(Path.Combine(_workingDirectory, TransportInitializedSpyFileName)), "Unable to verify that the queue creation logic was invoked");

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

        #region FakeTransport
        public class FakeTransportSpy : LearningTransport
        {
            public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
            {
                WriteFileAsFlagToBeVerifiedInTestAssertions(connectionString);
                return base.Initialize(settings, connectionString);
            }

            static void WriteFileAsFlagToBeVerifiedInTestAssertions(string connectionString) => File.WriteAllText(Path.Combine(connectionString, TransportInitializedSpyFileName), string.Empty);
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
                var transport = endpointConfig.UseTransport<FakeTransportSpy>();
                transport.ConnectionString(transportSettings.ConnectionString);
            }

            static void CustomizeEndpoint(RawEndpointConfiguration endpointConfig, TransportSettings transportSettings)
            {

                var transport = endpointConfig.UseTransport<FakeTransportSpy>();
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
