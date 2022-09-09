namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    class InstallationTests : PersistenceTestFixture
    {
        [Test]
        public void Should_write_expected_config_file()
        {
            PersistenceManifest persistenceManifest;

            using (var reader = new StreamReader(GetManifestPath()))
            {
                var manifestContent = reader.ReadToEnd();
                persistenceManifest = JsonSerializer.Deserialize<PersistenceManifest>(manifestContent);
            }

            var newInstance = new ServiceControlAuditNewInstance(new Version(1, 0, 0), persistenceManifest);

            var installPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "install");

            if (Directory.Exists(installPath))
            {
                Directory.Delete(installPath, true);
            }

            var dbPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "db");
            var logPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "log");

            newInstance.InstallPath = installPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.All.Single(t => t.Name == TransportNames.MSMQ);

            newInstance.DBPath = dbPath;
            newInstance.LogPath = logPath;
            newInstance.HostName = "localhost";
            newInstance.DatabaseMaintenancePort = 33333;

            newInstance.WriteConfigurationFile();

            var configFile = File.ReadAllText(Path.Combine(installPath, "ServiceControl.Audit.exe.config"));

            Approver.Verify(configFile, input => input.Replace(dbPath, "value-not-asserted").Replace(logPath, "value-not-asserted"));
        }
    }
}
