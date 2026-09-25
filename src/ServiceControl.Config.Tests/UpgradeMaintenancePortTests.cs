namespace ServiceControl.Config.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Commands;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    public class UpgradeMaintenancePortTests
    {
        static readonly Dictionary<string, bool> PrimaryUsesMaintenancePort = new()
        {
            ["RavenDB"] = true,
            ["RavenDB35"] = true,
            ["SQLServer"] = false,
            ["PostgreSQL"] = false
        };

        [Test]
        public void Every_shipped_primary_persister_gets_its_decided_maintenance_port_answer()
        {
            var manifests = ServiceControlPersisters.GetAllPrimaryManifests();

            Assert.That(manifests.Select(m => m.Name).Distinct(), Is.EquivalentTo(PrimaryUsesMaintenancePort.Keys),
                "The shipped persisters changed. Decide whether each new one needs the maintenance port prompt and add it to the table.");

            Assert.Multiple(() =>
            {
                foreach (var manifest in manifests)
                {
                    Assert.That(UpgradeServiceControlInstanceCommand.UsesMaintenancePort(manifest), Is.EqualTo(PrimaryUsesMaintenancePort[manifest.Name]), manifest.Name);
                }
            });
        }

        [Test]
        public void The_answer_follows_the_declared_setting_not_the_persister_name()
        {
            var ravenWithoutPort = new PersistenceManifest { Name = "RavenDB", Settings = [] };
            var otherWithPort = new PersistenceManifest
            {
                Name = "SomeFuturePersister",
                Settings = [new PersistenceManifest.Setting { Name = ServiceControlSettings.DatabaseMaintenancePort.Name }]
            };

            Assert.Multiple(() =>
            {
                Assert.That(UpgradeServiceControlInstanceCommand.UsesMaintenancePort(ravenWithoutPort), Is.False);
                Assert.That(UpgradeServiceControlInstanceCommand.UsesMaintenancePort(otherWithPort), Is.True);
            });
        }
    }
}
