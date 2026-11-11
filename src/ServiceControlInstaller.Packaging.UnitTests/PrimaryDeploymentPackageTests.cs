namespace Tests
{
    using System.IO;
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
                "RavenDB", "RavenDB7", "SQLServer", "RavenDB35", "PostgreSQL"
            };

            var persisters = deploymentPackage.DeploymentUnits.Where(u => u.Category == "Persisters");

            Assert.That(persisters.Select(d => d.Name), Is.EquivalentTo(expectedPersisters), $"Expected Persisters folder to contain {string.Join(',', expectedPersisters)}");

            foreach (var persister in persisters)
            {
                Assert.That(persister.Files.Any(f => f.Name.EndsWith(".config")), Is.False, $"{persister.Name} contains a config file");
                Assert.That(persister.Files.Any(f => f.Name == "persistence.manifest"), Is.True, $"{persister.Name} doesn't contain a persistence.manifest file");
            }
        }

        [Test]
        public void Raven_servers_should_be_included_outside_the_persisters()
        {
            var packageDirectory = deploymentPackage.Directory.FullName;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Path.Combine(packageDirectory, "Persisters", "RavenDB", "RavenDBServer"), Does.Not.Exist, "RavenDBServer should not be bundled inside the RavenDB persister");
                Assert.That(Path.Combine(packageDirectory, "Persisters", "RavenDB7", "RavenDBServer"), Does.Not.Exist, "RavenDBServer should not be bundled inside the RavenDB7 persister");
                Assert.That(Path.GetFullPath(Path.Combine(packageDirectory, "..", "RavenDBServer")), Does.Exist, "The RavenDB 6.2 server should be bundled as its own resource");
                Assert.That(Path.GetFullPath(Path.Combine(packageDirectory, "..", "RavenDB7Server")), Does.Exist, "The RavenDB 7.2 server should be bundled as its own resource");
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}