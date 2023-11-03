namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using System.Xml;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

    [TestFixture]
    class AuditInstanceTests : InstallationFixture
    {
        [Test]
        public void Should_default_to_raven35_when_no_config_entry_exists()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence(ZipFileFolder.FullName, "RavenDB35");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find(TransportNames.MSMQ);
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(ZipFilePath);
            newInstance.WriteConfigurationFile();

            //delete the setting to simulate an existing older instance
            var configPath = Path.Combine(InstallPath, "ServiceControl.Audit.exe.config");
            var existingConfigFile = new XmlDocument();
            existingConfigFile.Load(configPath);
            var entry = existingConfigFile.SelectSingleNode($"//add[@key='{AuditInstanceSettingsList.PersistenceType.Name}']");
            entry.ParentNode.RemoveChild(entry);
            existingConfigFile.Save(configPath);

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")), ZipFileFolder.FullName);

            instance.Reload();

            var persisterFilePath = Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDb.dll");

            //delete the persitence dll to make sure it gets re-installed
            File.Delete(persisterFilePath);

            instance.UpgradeFiles(ZipFilePath);
            FileAssert.Exists(persisterFilePath);
        }

        [Test]
        public void Should_update_existing_persister()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence(ZipFileFolder.FullName, "RavenDB5");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find(TransportNames.MSMQ);

            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(ZipFilePath);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")), ZipFileFolder.FullName);

            instance.Reload();

            var persisterFilePath = Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDb5.dll");

            //delete the persitence dll to make sure it gets re-installed
            File.Delete(persisterFilePath);

            instance.UpgradeFiles(ZipFilePath);
            FileAssert.Exists(persisterFilePath);
        }

        [Test]
        public void Should_remove_log_and_db_folders_on_uninstall()
        {

            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence(ZipFileFolder.FullName, "RavenDB35");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find(TransportNames.MSMQ);
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(ZipFilePath);
            newInstance.WriteConfigurationFile();

            var fakeRavenLogsPath = Path.Combine(InstallPath, "RavenLogs");
            newInstance.PersistenceManifest.Settings.Add(new PersistenceManifest.Setting
            {
                Name = "Raven/Esent/LogsPath",
                DefaultValue = fakeRavenLogsPath
            });

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")), ZipFileFolder.FullName);

            instance.Reload();

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.False(Directory.Exists(LogPath));
            Assert.False(Directory.Exists(DbPath));
            Assert.False(Directory.Exists(fakeRavenLogsPath));
        }

        class FakeWindowsServiceController : IWindowsServiceController
        {
            public FakeWindowsServiceController(string exePath)
            {
                ExePath = exePath;
            }
            public string ServiceName => "ServiceControl.Audit";

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
