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
                "RavenDb",
                "RavenDb5",
                "InMemory"};

            using (var zip = deploymentPackage.Open())
            {
                var storageFiles = zip.Entries.Where(e => e.FullName.StartsWith("Storages/")).Select(e => e.FullName).ToList();
                var storageFolders = storageFiles.Select(f => Directory.GetParent(f).Name).Distinct();

                CollectionAssert.AreEquivalent(allStorages, storageFolders, $"Expected storages folder to contain {string.Join(",", allStorages)}");
                Assert.IsFalse(storageFiles.Any(fn => fn.EndsWith(".config")));
                Assert.IsFalse(storageFiles.Any(fn => fn == "ServiceControl.Audit.Persistence.dll"));
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}