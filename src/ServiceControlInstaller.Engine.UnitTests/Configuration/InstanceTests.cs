namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
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
            Assert.That(instance.UsesEmbeddedDatabase, Is.True);

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(LogPath), Is.False);
            Assert.That(Directory.Exists(DbPath), Is.False);
        }

        [Test]
        public void Should_remove_only_message_body_folder_on_uninstall_for_SQL_Server()
        {
            var messageBodyPath = Path.Combine(Path.GetDirectoryName(DbPath), "bodies");
            var newInstance = CreateSqlServerInstance(new MessageBodyStorage
            {
                StorageType = MessageBodyStorageType.FileSystem,
                FileSystemStoragePath = messageBodyPath
            });

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.exe")));

            instance.Reload();

            Assert.That(Directory.Exists(DbPath), Is.False, "SQL Server instances have no local database folder");
            Assert.That(Directory.Exists(messageBodyPath), Is.True);
            Assert.That(instance.HasCleanableStoragePaths, Is.True);
            Assert.That(instance.UsesEmbeddedDatabase, Is.False);
            Assert.That(instance.ConfiguredMessageBodyStorageType, Is.EqualTo(MessageBodyStorageType.FileSystem));

            instance.RemoveLogsFolder();
            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(LogPath), Is.False);
            Assert.That(Directory.Exists(messageBodyPath), Is.False);
        }

        [Test]
        public void Should_not_attempt_local_cleanup_on_uninstall_for_external_storage()
        {
            var newInstance = CreateSqlServerInstance(new MessageBodyStorage
            {
                StorageType = MessageBodyStorageType.S3,
                S3BucketName = "bodies"
            });

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var instance = new ServiceControlInstance(new FakeWindowsServiceController(Path.Combine(InstallPath, "ServiceControl.exe")));

            instance.Reload();

            Assert.That(instance.HasCleanableStoragePaths, Is.False);
            Assert.That(instance.ConfiguredMessageBodyStorageType, Is.EqualTo(MessageBodyStorageType.S3));
            Assert.That(instance.AppConfig.Read<string>(ServiceControlSettings.MessageBodyFileSystemStoragePath, null), Is.Null, "Only the selected body storage type's settings are written");
        }

        ServiceControlNewInstance CreateSqlServerInstance(MessageBodyStorage messageBodyStorage)
        {
            var newInstance = ServiceControlNewInstance.CreateWithPersistence("SQLServer");

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.PersistenceConnectionString = "placeholder-connection-string";
            newInstance.MessageBodyStorage = messageBodyStorage;

            return newInstance;
        }

        [Test]
        public void Should_treat_external_RavenDB_as_unmanaged_storage()
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
            instance.AppConfig.Config.AppSettings.Settings.Add(ServiceControlSettings.RavenDBConnectionString.Name, "http://ravendb.example:8080");
            instance.AppConfig.Config.Save();

            instance.Reload();

            Assert.That(instance.UsesEmbeddedDatabase, Is.False);
            Assert.That(instance.HasCleanableStoragePaths, Is.False);

            instance.RemoveDataBaseFolder();

            Assert.That(Directory.Exists(DbPath), Is.True);
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
