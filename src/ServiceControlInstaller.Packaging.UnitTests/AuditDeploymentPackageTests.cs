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
                "PostgreSQL",
                "RavenDB35", // Still must exist, as Raven35 persistence.manifest file must be available for SCMU to understand old versions
                "RavenDB"
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
        public void Raven_server_should_be_included()
        {
            var inPersisterPath = Path.Combine(deploymentPackage.Directory.FullName, "Persisters", "RavenDB", "RavenDBServer");
            var separateAssetPath = Path.GetFullPath(Path.Combine(deploymentPackage.Directory.FullName, "..", "RavenDBServer"));

            Assert.Multiple(() =>
            {
                Assert.That(inPersisterPath, Does.Not.Exist, "RavenDBServer should not be bundled inside the persister");
                Assert.That(separateAssetPath, Does.Exist, "RavenDBServer should be bundled as its own resource");
            });
        }

        readonly DeploymentPackage deploymentPackage;
    }
}