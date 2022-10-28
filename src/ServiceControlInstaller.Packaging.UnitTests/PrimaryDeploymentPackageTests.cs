namespace Tests
{
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
    public class PrimaryDeploymentPackageTests
    {
        public PrimaryDeploymentPackageTests()
        {
            var fileName = DeploymentPackage.GetZipFolder().EnumerateFiles("*.zip")
                .Single(f => f.Name.StartsWith("Particular.ServiceControl-"));

            deploymentPackage = new DeploymentPackage(fileName);
        }

        [Test]
        public void Should_bundle_ravendb35_persister()
        {
            using (var zip = deploymentPackage.Open())
            {
                Assert.IsNotNull(zip.Entries.SingleOrDefault(e => e.FullName == $"ServiceControl/ServiceControl.Persistence.RavenDb.dll"), $"RavenDB 3.5 persister should be bundled");
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}