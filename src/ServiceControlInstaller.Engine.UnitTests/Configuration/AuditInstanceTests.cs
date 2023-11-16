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
        const string zipResourceName = "Particular.ServiceControl.Audit.zip";

        [Test]
        public void Should_default_to_raven35_when_no_config_entry_exists()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence("RavenDB35");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            //delete the setting to simulate an existing older instance
            var configPath = Path.Combine(InstallPath, "ServiceControl.Audit.exe.config");
            var existingConfigFile = new XmlDocument();
            existingConfigFile.Load(configPath);
            var entry = existingConfigFile.SelectSingleNode($"//add[@key='{AuditInstanceSettingsList.PersistenceType.Name}']");
            entry.ParentNode.RemoveChild(entry);
            existingConfigFile.Save(configPath);

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")));

            instance.Reload();

            instance.UpgradeFiles(zipResourceName);
            // Don't want any persistence DLL, bit awkward but serves the purpose of the test
            FileAssert.DoesNotExist(Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDb.dll"));
            FileAssert.DoesNotExist(Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDB.dll"));
            FileAssert.DoesNotExist(Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDb5.dll"));

            var manifestFilePath = Path.Combine(InstallPath, "persistence.manifest");
            var manifestText = File.ReadAllText(manifestFilePath);
            StringAssert.Contains("RavenDB 3.5", manifestText);
        }

        [Test]
        public void Should_update_existing_persister()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence("RavenDB");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");

            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlAuditInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.Audit.exe")));

            instance.Reload();

            var persisterFilePath = Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDB.dll");

            //delete the persitence dll to make sure it gets re-installed
            File.Delete(persisterFilePath);

            instance.UpgradeFiles(zipResourceName);
            FileAssert.Exists(persisterFilePath);
        }

        [Test]
        public void Should_remove_log_and_db_folders_on_uninstall()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithPersistence("RavenDB35");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("MSMQ");
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

            Assert.False(Directory.Exists(LogPath));
            Assert.False(Directory.Exists(DbPath));
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
