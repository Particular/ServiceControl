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

            var persisterFolders = deploymentPackage.Directory.GetDirectories("Persisters/*");

            CollectionAssert.AreEquivalent(expectedPersisters, persisterFolders.Select(d => d.Name), $"Expected persisters folder to contain {string.Join(",", expectedPersisters)}");

            foreach (var persisterFolder in persisterFolders)
            {
                var persisterFiles = persisterFolder.EnumerateFiles();

                Assert.IsFalse(persisterFiles.Any(f => f.Name.EndsWith(".config")), $"{persisterFolder} contains a config file");
                Assert.IsFalse(persisterFiles.Any(f => f.Name == "ServiceControl.Audit.Persistence.dll"), $"{persisterFolder} contains the transport seam assembly");
                Assert.IsTrue(persisterFiles.Any(f => f.Name == "persistence.manifest"), $"{persisterFolder} doesn't contain a persistence.manifest file");
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