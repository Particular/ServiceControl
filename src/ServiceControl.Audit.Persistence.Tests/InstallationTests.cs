namespace ServiceControl.Audit.Persistence.Tests.RavenDB
{
    using System;
    using System.IO;
    using System.IO.Compression;
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
            var zipFileFolder = GetZipFolder();

            var zipFilePath = zipFileFolder.EnumerateFiles("*.zip")
                .Single(f => f.Name.Contains(".Audit"))
                .FullName;

            var newInstance = new ServiceControlAuditNewInstance
            {
                Version = new Version(1, 0, 0)
            };
            var installPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "install");
            var dbPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "db");
            var logPath = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID, "log");

            newInstance.InstallPath = installPath;
            newInstance.TransportPackage = ServiceControlCoreTransports.All.Single(t => t.Name == TransportNames.MSMQ);

            using (var zipArchive = ZipFile.OpenRead(zipFilePath))
            {
                var manifestEntry = zipArchive.GetEntry($"Storages/{ZipName}/manifest.json");

                Assert.NotNull(manifestEntry, $"{ZipName} contains no manifest file");

                using (var reader = new StreamReader(manifestEntry.Open()))
                {
                    var manifestContent = reader.ReadToEnd();
                    newInstance.PersistenceManifest = JsonSerializer.Deserialize<PersistenceManifest>(manifestContent);
                }
            }

            newInstance.DBPath = dbPath;
            newInstance.LogPath = logPath;

            newInstance.WriteConfigurationFile();

            var configFile = File.ReadAllText(Path.Combine(installPath, "ServiceControl.Audit.exe.config"));

            Approver.Verify(configFile, input => input.Replace(dbPath, "value-not-asserted").Replace(logPath, "value-not-asserted"));
        }
    }
}
