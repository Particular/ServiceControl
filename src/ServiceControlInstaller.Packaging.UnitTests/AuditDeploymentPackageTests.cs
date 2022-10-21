namespace Tests
{
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    public class AuditDeploymentPackageTests
    {
        public AuditDeploymentPackageTests()
        {
            var fileName = DeploymentPackage.GetZipFolder().EnumerateFiles("*.zip")
                .Single(f => f.Name.Contains(".Audit"));

            deploymentPackage = new DeploymentPackage(fileName);
        }

        [Test]
        public void Should_package_storages_individually()
        {
            var allStorages = new string[] {
                "RavenDB35",
                "RavenDb5",
                "InMemory"};

            using (var zip = deploymentPackage.Open())
            {
                var persisterFiles = zip.Entries
                    .Where(e => e.FullName.StartsWith("Persisters/"))
                    .Where(WillEndUpInInstallationFolder)
                    .Select(e => e.FullName).ToList();
                var persisterFolders = persisterFiles.Select(f => Directory.GetParent(f).Name).Distinct();

                CollectionAssert.AreEquivalent(allStorages, persisterFolders, $"Expected persisters folder to contain {string.Join(",", allStorages)}");
                Assert.IsFalse(persisterFiles.Any(fn => fn.EndsWith(".config")));
                Assert.IsFalse(persisterFiles.Any(fn => fn == "ServiceControl.Audit.Persistence.dll"));

                foreach (var persisterFolder in persisterFolders)
                {
                    Assert.IsNotNull(zip.Entries.SingleOrDefault(e => e.FullName == $"Persisters/{persisterFolder}/persistence.manifest"), $"{persisterFolder} doesn't contain a persistence.manifest file");
                }
            }
        }

        static bool WillEndUpInInstallationFolder(ZipArchiveEntry entry)
        {
            // Persisters/<name>/<filename.ext>
            return entry.FullName.Count(c => c == '/') == 2;
        }

        [Test]
        public void Raven5_should_include_raven_server()
        {
            var storage = "RavenDb5";

            using (var zip = deploymentPackage.Open())
            {
                var persisterFiles = zip.Entries.Where(e => e.FullName.StartsWith("Persisters/") && e.FullName.Contains(storage)).Select(e => e.FullName).ToList();
                var persisterFolders = persisterFiles.Select(f => Directory.GetParent(f).Name).Distinct();

                Assert.IsTrue(persisterFiles.Any(fn => fn.Contains("RavenDBServer")));
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}