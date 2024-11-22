namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.FileSystem;
    using ServiceControlInstaller.Engine.Instances;

    class InstallationTests
    {
        [Test]
        [TestCaseSource(nameof(GetAuditPersistenceManifestPaths))]
        public void Audit_install_should_write_expected_config_file(string manifestPath)
        {
            var installPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "install");
            var dbPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "db");
            var logPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "log");

            try
            {
                PersistenceManifest persistenceManifest;

                using (var reader = new StreamReader(manifestPath))
                {
                    var manifestContent = reader.ReadToEnd();
                    persistenceManifest = JsonSerializer.Deserialize<PersistenceManifest>(manifestContent);
                }

                if (!persistenceManifest.IsSupported)
                {
                    Assert.Ignore("Don't care about config for unsupported persistence types.");
                }

                var newInstance = new ServiceControlAuditNewInstance(persistenceManifest);


                if (Directory.Exists(installPath))
                {
                    Directory.Delete(installPath, true);
                }

                newInstance.InstallPath = installPath;
                newInstance.TransportPackage = ServiceControlCoreTransports.Find("LearningTransport");

                newInstance.DBPath = dbPath;
                newInstance.LogPath = logPath;
                newInstance.HostName = "localhost";
                newInstance.DatabaseMaintenancePort = 33333;

                newInstance.WriteConfigurationFile();

                var configFile = File.ReadAllText(Path.Combine(installPath, "ServiceControl.Audit.exe.config"));
                var scenario = Path.GetFileName(Path.GetDirectoryName(manifestPath));

                Approver.Verify(configFile, input => input.Replace(dbPath, "value-not-asserted").Replace(logPath, "value-not-asserted"), scenario);
            }
            finally
            {
                FileUtils.DeleteDirectory(installPath, true, false);
                FileUtils.DeleteDirectory(dbPath, true, false);
                FileUtils.DeleteDirectory(logPath, true, false);
            }
        }

        public static string[][] GetAuditPersistenceManifestPaths()
        {
            var deployPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "deploy"));
            var persistersPath = Path.Combine(deployPath, "Particular.ServiceControl.Audit", "Persisters");
            var manifestPaths = Directory.GetFiles(persistersPath, "persistence.manifest", SearchOption.AllDirectories);

            return manifestPaths
                .Select(path => new string[] { path })
                .ToArray();
        }
    }
}