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
        public void Should_bundle_ravendb35_persister()
        {
            var persistenceAssemblyPath = Path.Combine(deploymentPackage.Directory.FullName, "Persisters", "RavenDB35", "ServiceControl.Persistence.RavenDb.dll");
            FileAssert.Exists(persistenceAssemblyPath, "RavenDB 3.5 persister should be bundled");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}