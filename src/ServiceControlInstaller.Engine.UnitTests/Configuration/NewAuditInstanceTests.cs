namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.IO;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class NewAuditInstanceTests : InstallationFixture
    {
        const string zipResourceName = "Particular.ServiceControl.Audit.zip";

        [Test]
        public void Should_install_modern_raven_for_new_instances()
        {
            var newInstance = ServiceControlAuditNewInstance.CreateWithDefaultPersistence();

            newInstance.InstallPath = InstallPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");

            newInstance.DBPath = DbPath;
            newInstance.LogPath = LogPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.CopyFiles(zipResourceName);
            newInstance.WriteConfigurationFile();

            var configFile = File.ReadAllText(Path.Combine(InstallPath, "ServiceControl.Audit.exe.config"));

            Approver.Verify(configFile, input => input.Replace(DbPath, "value-not-asserted").Replace(LogPath, "value-not-asserted"));
        }
    }
}