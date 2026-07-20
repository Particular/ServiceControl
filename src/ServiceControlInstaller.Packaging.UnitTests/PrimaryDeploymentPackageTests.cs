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
                "RavenDB", "SQLServer", "RavenDB35", "PostgreSQL"
            };

            var persisters = deploymentPackage.DeploymentUnits.Where(u => u.Category == "Persisters");

            Assert.That(persisters.Select(d => d.Name), Is.EquivalentTo(expectedPersisters), $"Expected Persisters folder to contain {string.Join(',', expectedPersisters)}");

            foreach (var persister in persisters)
            {
                Assert.That(persister.Files.Any(f => f.Name.EndsWith(".config")), Is.False, $"{persister.Name} contains a config file");
                Assert.That(persister.Files.Any(f => f.Name == "persistence.manifest"), Is.True, $"{persister.Name} doesn't contain a persistence.manifest file");
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}