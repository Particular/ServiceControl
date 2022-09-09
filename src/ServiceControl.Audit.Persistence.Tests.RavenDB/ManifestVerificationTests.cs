namespace ServiceControl.Audit.Persistence.Tests.RavenDB
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class ManifestVerificationTests : PersistenceTestFixture
    {
        [Test]
        public void Verify()
        {
            using (var reader = new StreamReader(GetManifestPath()))
            {
                var manifestContent = reader.ReadToEnd();
                var manifest = JsonSerializer.Deserialize<PersistenceManifest>(manifestContent);

                Assert.AreEqual(manifest.Version, "1.0.0");
                Assert.AreEqual(manifest.Name, "RavenDb35");

                var cleanupSetting = manifest.Settings.Single(s => s.Name == "ServiceControl/Audit/RavenDb35/RunCleanupBundle");

                Assert.AreEqual("true", cleanupSetting.DefaultValue);

                var pageSizeSetting = manifest.Settings.Single(s => s.Name == "Raven/ESENT/MaxPageSize");

                Assert.AreEqual("4096", pageSizeSetting.DefaultValue);

                var dbPathSetting = manifest.Settings.Single(s => s.Name == "ServiceControl.Audit/DBPath");

                Assert.True(dbPathSetting.Mandatory);

                var hostNameSetting = manifest.Settings.Single(s => s.Name == "ServiceControl.Audit/HostName");

                Assert.True(hostNameSetting.Mandatory);

                var databaseMaintenancePortSetting = manifest.Settings.Single(s => s.Name == "ServiceControl.Audit/DatabaseMaintenancePort");

                Assert.True(databaseMaintenancePortSetting.Mandatory);

                CollectionAssert.AreEquivalent(new[]
                {
                    "Raven/IndexStoragePath",
                    "Raven/CompiledIndexCacheDirectory",
                    "Raven/Esent/LogsPath",
                    "ServiceControl.Audit/DBPath"
                }, manifest.SettingsWithPathsToCleanup);
            }
        }
    }
}
