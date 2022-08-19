namespace Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    [TestFixtureSource(typeof(DeploymentPackage), nameof(DeploymentPackage.All))]
    public class DeploymentPackageTests
    {
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

                var entries = mainEntries
                    .Join(zip.Entries.Except(mainEntries), mainEntry => mainEntry.Name, entry => entry.Name, (mainEntry, entry) => new { mainEntry, entry })
                    .Where(t => t.entry.Length != t.mainEntry.Length && !IgnoreList.Contains(@t.entry.FullName))
                    .Select(t => t.entry.FullName)
                    .ToList();

                CollectionAssert.IsEmpty(entries, $"File sizes should match the ones in the {deploymentPackage.ServiceName} folder. Check versions of dependencies.");
            }
        }

        static IEnumerable<string> IgnoreList
        {
            get
            {
                // NServiceBus.Transport.Msmq references V5.0.0 of this assembly,
                // but only for the native delayed delivery codepath so is not used by ServiceControl.
                // Updating the reference will in turn break NServiceBus.Transport.RabbitMQ
                // as that package references V4.7.1.
                // It should therefore be safe to explicitly exclude this assembly mismatch from
                // the test.
                yield return "Transports/MSMQ/System.Runtime.CompilerServices.Unsafe.dll";
                yield return "Transports/MSMQ/System.Threading.Channels.dll";
            }
        }

        readonly DeploymentPackage deploymentPackage;
    }
}