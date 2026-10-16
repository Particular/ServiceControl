namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

    [TestFixture]
    class InstanceTests : InstallationFixture
    {
        const string zipResourceName = "Particular.ServiceControl.zip";

        [Test]
        public void Should_remove_db_folder_on_uninstall_for_RavenDB()
        {
            var newInstance = ServiceControlNewInstance.CreateWithPersistence("RavenDB");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.exe")));

            instance.Reload();

            Assert.That(instance.HasCleanableStoragePaths, Is.True);

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(LogPath), Is.False);
            Assert.That(Directory.Exists(DbPath), Is.False);
        }

        [Test]
        public void Should_not_attempt_local_cleanup_on_uninstall_for_external_storage()
        {
            var newInstance = ServiceControlNewInstance.CreateWithPersistence("SQLServer");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;
            newInstance.PersistenceConnectionString = "placeholder-connection-string";

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.exe")));

            instance.Reload();

            Assert.That(instance.HasCleanableStoragePaths, Is.False);

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(LogPath), Is.False, "Logs should still be removed regardless of storage type");
            Assert.That(Directory.Exists(DbPath), Is.True, "Nothing local backs a SQL Server instance, so its unrelated DBPath folder must be left alone");
        }

        class FakeWindowsServiceController : IWindowsServiceController
        {
            public FakeWindowsServiceController(string exePath)
            {
                ExePath = exePath;
            }
            public string ServiceName => "ServiceControl";

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
