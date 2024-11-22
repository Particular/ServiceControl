namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

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