namespace Tests
{
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    public class PrimaryDeploymentPackageTests
    {
        public PrimaryDeploymentPackageTests()
        {
            deploymentPackage = DeploymentPackage.All.Single(d => d.ServiceName == "ServiceControl");
        }

        [Test]
        public void Should_package_storages_individually()
        {
            var expectedPersisters = new[] {
                "RavenDB35", // Still must exist, as Raven35 persistence.manifest file must be available for SCMU to understand old versions
                "RavenDB5"
            };

            var persisters = deploymentPackage.DeploymentUnits.Where(u => u.Category == "Persisters");

            CollectionAssert.AreEquivalent(expectedPersisters, persisters.Select(d => d.Name), $"Expected persisters folder to contain {string.Join(",", expectedPersisters)}");

            foreach (var persister in persisters)
            {
                Assert.IsFalse(persister.Files.Any(f => f.Name.EndsWith(".config")), $"{persister.Name} contains a config file");
                Assert.IsTrue(persister.Files.Any(f => f.Name == "persistence.manifest"), $"{persister.Name} doesn't contain a persistence.manifest file");
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}