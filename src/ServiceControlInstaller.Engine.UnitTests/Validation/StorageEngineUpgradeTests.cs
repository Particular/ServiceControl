namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System.Collections.Generic;
    using System.Linq;
    using Instances;
    using NUnit.Framework;

    [TestFixture]
    public class StorageEngineUpgradeTests
    {
        static readonly Dictionary<string, bool> PrimaryUpgradableInPlace = new()
        {
            ["RavenDB"] = true,
            ["RavenDB35"] = false,
            ["SQLServer"] = true,
            ["PostgreSQL"] = true
        };

        static readonly Dictionary<string, bool> AuditUpgradableInPlace = new()
        {
            ["RavenDB"] = true,
            ["RavenDB35"] = false
        };

        [Test]
        public void Every_shipped_primary_storage_engine_gets_its_decided_upgrade_answer() =>
            AssertDecidedAnswers(ServiceControlPersisters.GetAllPrimaryManifests(), PrimaryUpgradableInPlace);

        [Test]
        public void Every_shipped_audit_storage_engine_gets_its_decided_upgrade_answer() =>
            AssertDecidedAnswers(ServiceControlPersisters.GetAllAuditManifests(), AuditUpgradableInPlace);

        // A null persistence type is how a version 4 config with no PersistenceType setting resolves, which is RavenDB 3.5.
        [TestCase(null, ExpectedResult = false)]
        [TestCase("NotAKnownPersister", ExpectedResult = false)]
        public bool Whether_an_unlisted_primary_persistence_type_can_be_upgraded_in_place(string persistenceType) =>
            AbstractCommandChecks.IsUpgradableStorageEngine(ServiceControlPersisters.GetPrimaryPersistence(persistenceType));

        [TestCase(null, ExpectedResult = false)]
        [TestCase("NotAKnownPersister", ExpectedResult = false)]
        [TestCase("ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5", ExpectedResult = true)]
        [TestCase("ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb", ExpectedResult = false)]
        public bool Whether_an_unlisted_audit_persistence_type_can_be_upgraded_in_place(string persistenceType) =>
            AbstractCommandChecks.IsUpgradableStorageEngine(ServiceControlPersisters.GetAuditPersistence(persistenceType));

        [Test]
        public void The_answer_follows_whether_the_manifest_is_supported_not_its_name()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AbstractCommandChecks.IsUpgradableStorageEngine(new PersistenceManifest { Name = "SomeFuturePersister", IsSupported = true }), Is.True);
                Assert.That(AbstractCommandChecks.IsUpgradableStorageEngine(new PersistenceManifest { Name = "RavenDB", IsSupported = false }), Is.False);
            });
        }

        static void AssertDecidedAnswers(PersistenceManifest[] manifests, Dictionary<string, bool> decided)
        {
            Assert.That(manifests.Select(m => m.Name).Distinct(), Is.EquivalentTo(decided.Keys),
                "The shipped persisters changed. Decide whether each new one can be upgraded in place and add it to the table.");

            Assert.Multiple(() =>
            {
                foreach (var manifest in manifests)
                {
                    Assert.That(AbstractCommandChecks.IsUpgradableStorageEngine(manifest), Is.EqualTo(decided[manifest.Name]), manifest.Name);
                }
            });
        }
    }
}
