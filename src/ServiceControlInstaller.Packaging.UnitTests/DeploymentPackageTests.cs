namespace Tests
{
    using System.Linq;
    using NUnit.Framework;

    [TestFixtureSource(typeof(DeploymentPackage), nameof(DeploymentPackage.All))]
    public class DeploymentPackageTests
    {
        DeploymentPackage deploymentPackage;

        public DeploymentPackageTests(DeploymentPackage deploymentPackage)
        {
            this.deploymentPackage = deploymentPackage;
        }

        [Test]
        public void DuplicateAssemblyFileSizesShouldMatch()
        {
            using (var zip = deploymentPackage.Open())
            {
                var mainEntries = zip.Entries.Where(x => x.FullName.StartsWith(deploymentPackage.ServiceName)).ToList();

                CollectionAssert.IsNotEmpty(mainEntries, $"Expected a {deploymentPackage.ServiceName} folder in {deploymentPackage.FullName}");

                var entriesToValidate = zip.Entries.Except(mainEntries).ToList();

                CollectionAssert.IsEmpty(
                    from mainEntry in mainEntries
                    join entry in zip.Entries.Except(mainEntries) on mainEntry.Name equals entry.Name
                    where entry.Length != mainEntry.Length &&
                        entry.FullName != "Transports/MSMQ/System.Threading.Channels.dll" // NServiceBus.Transport.Msmq references V5.0.0 of this assembly,
                                                                                          // but only for the native delayed delivery codepath so is not used by ServiceControl.
                                                                                          // Updating the reference will in turn break NServiceBus.Transport.RabbitMQ
                                                                                          // as that package references V4.7.1.
                                                                                          // It should therefore be safe to explicitly exclude this assembly mismatch from
                                                                                          // the test.
                    select entry.FullName,
                    $"File sizes should match the ones in the {deploymentPackage.ServiceName} folder. Check versions of dependencies.");
            }
        }
    }
}