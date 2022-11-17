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
        public void Should_bundle_ravendb35_persister()
        {
            FileAssert.Exists($"{deploymentPackage.Directory.FullName}/ServiceControl/ServiceControl.Persistence.RavenDb.dll", "RavenDB 3.5 persister should be bundled");
        }

        readonly DeploymentPackage deploymentPackage;
    }
}