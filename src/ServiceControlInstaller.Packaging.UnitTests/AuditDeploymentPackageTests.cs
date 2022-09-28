namespace Tests
{
    using System.IO;
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
                "RavenDb35",
                "RavenDb5",
                "InMemory"};

            using (var zip = deploymentPackage.Open())
            {
                var persisterFiles = zip.Entries.Where(e => e.FullName.StartsWith("Persisters/")).Select(e => e.FullName).ToList();
                var persisterFolders = persisterFiles.Select(f => Directory.GetParent(f).Name).Distinct();

                CollectionAssert.AreEquivalent(allStorages, persisterFolders, $"Expected persisters folder to contain {string.Join(",", allStorages)}");
                Assert.IsFalse(persisterFiles.Any(fn => fn.EndsWith(".config")));
                Assert.IsFalse(persisterFiles.Any(fn => fn == "ServiceControl.Audit.Persistence.dll"));

                foreach (var persisterFolder in persisterFolders)
                {
                    Assert.IsNotNull(zip.Entries.SingleOrDefault(e => e.FullName == $"Persisters/{persisterFolder}/manifest.json"), $"{persisterFolder} doesn't contain a manifest.json file");
                }
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}