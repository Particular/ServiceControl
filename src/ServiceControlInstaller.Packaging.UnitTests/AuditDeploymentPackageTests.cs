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
            deploymentPackage = DeploymentPackage.All.Single(d => d.ServiceName.Contains("Audit"));
        }

        [Test]
        public void Should_package_storages_individually()
        {
            var expectedPersisters = new[] {
                "RavenDB35", // Still must exist, as Raven35 persistence.manifest file must be available for SCMU to understand old versions
                "RavenDB"
            };

            var persisters = deploymentPackage.DeploymentUnits.Where(u => u.Category == "Persisters");

            CollectionAssert.AreEquivalent(expectedPersisters, persisters.Select(d => d.Name), $"Expected Persisters folder to contain {string.Join(",", expectedPersisters)}");

            foreach (var persister in persisters)
            {
                Assert.IsFalse(persister.Files.Any(f => f.Name.EndsWith(".config")), $"{persister.Name} contains a config file");
                Assert.IsTrue(persister.Files.Any(f => f.Name == "persistence.manifest"), $"{persister.Name} doesn't contain a persistence.manifest file");
            }
        }

        [Test]
        public void Raven_server_should_be_included()
        {
            var inPersisterPath = Path.Combine(deploymentPackage.Directory.FullName, "Persisters", "RavenDB", "RavenDBServer");
            var separateAssetPath = Path.GetFullPath(Path.Combine(deploymentPackage.Directory.FullName, "..", "RavenDBServer"));

            DirectoryAssert.DoesNotExist(inPersisterPath, "RavenDBServer should not be bundled inside the persister");
            DirectoryAssert.Exists(separateAssetPath, "RavenDBServer should be bundled as its own resource");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}