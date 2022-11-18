namespace Tests
{
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
            var expectedPersisters = new string[] {
                "RavenDB35",
                "RavenDB5",
                "InMemory"};

            var persisters = deploymentPackage.DeploymentUnits.Where(u => u.Category == "Persisters");

            CollectionAssert.AreEquivalent(expectedPersisters, persisters.Select(d => d.Name), $"Expected persisters folder to contain {string.Join(",", expectedPersisters)}");

            foreach (var persister in persisters)
            {
                Assert.IsFalse(persister.Files.Any(f => f.Name.EndsWith(".config")), $"{persister.Name} contains a config file");
                Assert.IsFalse(persister.Files.Any(f => f.Name == "ServiceControl.Audit.Persistence.dll"), $"{persister.Name} contains the transport seam assembly");
                Assert.IsTrue(persister.Files.Any(f => f.Name == "persistence.manifest"), $"{persister.Name} doesn't contain a persistence.manifest file");
            }
        }

        [Test]
        public void Raven5_should_include_raven_server()
        {
            DirectoryAssert.Exists($"{deploymentPackage.Directory.FullName}/Persisters/RavenDB5/RavenDBServer", "RavenDBServer should be bundled");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}