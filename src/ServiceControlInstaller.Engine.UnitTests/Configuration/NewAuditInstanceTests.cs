namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class NewAuditInstanceTests : InstallationFixture
    {
        [Test]
        public void Should_install_raven5_for_new_instances()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithDefaultPersistence(ZipFileFolder.FullName);

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.All.Single(t => t.Name == TransportNames.MSMQ);

            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(ZipFilePath);
            newInstance.WriteConfigurationFile();

            FileAssert.Exists(Path.Combine(InstallPath, "ServiceControl.Audit.Persistence.RavenDb5.dll"));
            var configFile = File.ReadAllText(Path.Combine(InstallPath, "ServiceControl.Audit.exe.config"));

            Approver.Verify(configFile, input => input.Replace(DbPath, "value-not-asserted").Replace(LogPath, "value-not-asserted"));
        }

        [Test]
        public void Should_parse_all_persister_manifests()
        {
            var zipFiles = ZipFileFolder.EnumerateFiles();

            var zipFile = zipFiles.Single(f => f.Name.StartsWith("Particular.ServiceControl.Audit"));

            var allManifests = ServiceControlAuditPersisters.LoadAllManifests(zipFile.FullName);

            CollectionAssert.IsNotEmpty(allManifests);
        }
    }
}
