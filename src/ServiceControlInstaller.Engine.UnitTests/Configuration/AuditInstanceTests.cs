namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;
    using ServiceControlInstaller.Engine.Validation;

    [TestFixture]
    class AuditInstanceTests : InstallationFixture
    {
        const string zipResourceName = "Particular.ServiceControl.Audit.zip";

        [Test]
        public void Should_update_existing_persister()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence("RavenDB");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");

            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")));
            var configFile = Path.Combine(InstallPath, "ServiceControl.Audit.exe.config");
            var originalConfigContents = File.ReadAllText(configFile);

            instance.UpgradeFiles(zipResourceName);

            Assert.That(configFile, Does.Exist);

            var upgradedConfigContents = File.ReadAllText(configFile);

            Assert.That(upgradedConfigContents, Is.EqualTo(originalConfigContents));
        }

        [TestCase("RavenDB7", "RavenDB7", "RavenDB", 72_002)]
        [TestCase("RavenDB", "RavenDB", "RavenDB7", 62_000)]
        [TestCase("SQLServer", "RavenDB", "RavenDB7", 62_000)]
        [TestCase("PostgreSQL", "RavenDB", "RavenDB7", 62_000)]
        public void Should_install_only_the_server_that_matches_the_persister(string persistence, string serverPersisterFolder, string otherPersisterFolder, int expectedDocumentsVersion)
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence(persistence);

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);

            AssertOnlyServerInstalled(serverPersisterFolder, otherPersisterFolder, expectedDocumentsVersion);
        }

        [TestCase("RavenDB7", "RavenDB7", "RavenDB", 72_002)]
        [TestCase("RavenDB", "RavenDB", "RavenDB7", 62_000)]
        public void Upgrade_should_keep_the_persister_and_the_server_that_matches_it(string persistence, string serverPersisterFolder, string otherPersisterFolder, int expectedDocumentsVersion)
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence(persistence);

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")));
            instance.Reload();

            instance.UpgradeFiles(zipResourceName);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(instance.PersistenceManifest.Name, Is.EqualTo(persistence));
                Assert.That(File.ReadAllText(Path.Combine(InstallPath, "ServiceControl.Audit.exe.config")), Does.Contain($"value=\"{persistence}\""));
            }

            AssertOnlyServerInstalled(serverPersisterFolder, otherPersisterFolder, expectedDocumentsVersion);
        }

        void AssertOnlyServerInstalled(string serverPersisterFolder, string otherPersisterFolder, int expectedDocumentsVersion)
        {
            var serverAssembly = Path.Combine(InstallPath, "Persisters", serverPersisterFolder, "RavenDBServer", "Raven.Server.dll");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(serverAssembly, Does.Exist);
                Assert.That(Path.Combine(InstallPath, "Persisters", otherPersisterFolder, "RavenDBServer"), Does.Not.Exist);

                using var stream = File.OpenRead(serverAssembly);
                Assert.That(RavenDBStorageSchema.Read(stream)?.Documents, Is.EqualTo(expectedDocumentsVersion));
            }
        }

        [Test]
        public void Should_remove_log_and_db_folders_on_uninstall()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence("RavenDB35");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")));

            instance.Reload();

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(LogPath), Is.False);
            Assert.That(Directory.Exists(DbPath), Is.False);
        }

        class FakeWindowsServiceController : IWindowsServiceController
        {
            public FakeWindowsServiceController(string exePath)
            {
                ExePath = exePath;
            }
            public string ServiceName => "ServiceControl.Audit";

            public bool Exists() => true;

            public string ExePath { get; }

            public string Description { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public ServiceControllerStatus Status => throw new NotImplementedException();

            public string Account => "system";

            public string DisplayName => throw new NotImplementedException();

            public void ChangeAccountDetails(string accountName, string serviceAccountPwd) => throw new NotImplementedException();
            public void Delete() => throw new NotImplementedException();
            public void Refresh()
            { }
            public void SetStartupMode(string v) => throw new NotImplementedException();
            public void Start() => throw new NotImplementedException();
            public void Stop() => throw new NotImplementedException();
            public void WaitForStatus(ServiceControllerStatus stopped, TimeSpan timeSpan) => throw new NotImplementedException();
        }
    }
}
