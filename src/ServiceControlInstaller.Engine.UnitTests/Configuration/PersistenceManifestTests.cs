namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    public class PersistenceManifestTests
    {
        /*
         * Persistence methods that are no longer supported still need to ship a persistence.manifest file so that
         * ServiceControl Management can still display information about older ServiceControl instances.
         */

        [Test]
        public void ApprovePrimaryInstanceManifests()
        {
            Approver.Verify(ServiceControlPersisters.GetAllPrimaryManifests().Select(m => $"{m.Name}: {m.DisplayName}"));
        }

        [Test]
        public void ApproveAuditInstanceManifests()
        {
            Approver.Verify(ServiceControlPersisters.GetAllAuditManifests().Select(m => $"{m.Name}: {m.DisplayName}"));
        }

        [Test]
        public void ServiceControl4AuditTypeNames()
        {
            void Test(string v4TypeName, string expectedPersisterName)
            {
                var manifest = ServiceControlPersisters.GetAuditPersistence(v4TypeName);
                Assert.That(manifest?.Name ?? "not found", Is.EqualTo(expectedPersisterName));
            }

            // ServiceControl v4 audit instances created with RavenDB 5 should have this configured type, which in V5 should be considered RavenDB (modern)
            Test("ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5", "RavenDB");

            // ServiceControl v4 audit instances on RavenDB 3.5 should have this type, with the improperly lowercase "b" on RavenDb
            Test("ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb", "RavenDB35");

            // This is the actual type for the v5 RavenDB 5+ persistence and it should return correctly as well
            Test("ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDB", "RavenDB");
        }

        [Test]
        public void Shipped_primary_manifests_resolve_the_RavenDB_persisters_by_name_alias_and_type_name()
        {
            Assert.Multiple(() =>
            {
                foreach (var persistenceType in new[] { "RavenDB", "ravendb", "RavenDB6", "ravendb6", RavenDBTypeName })
                {
                    Assert.That(ServiceControlPersisters.GetPrimaryPersistence(persistenceType).DisplayName, Is.EqualTo("RavenDB 6"), persistenceType);
                }

                Assert.That(ServiceControlPersisters.GetPrimaryPersistence("RavenDB7").DisplayName, Is.EqualTo("RavenDB 7"));
                Assert.That(ServiceControlPersisters.GetPrimaryPersistence("ravendb7").DisplayName, Is.EqualTo("RavenDB 7"));
            });
        }

        [Test]
        public void Shipped_audit_manifests_resolve_the_RavenDB_persisters_by_name_alias_and_type_name()
        {
            Assert.Multiple(() =>
            {
                foreach (var persistenceType in new[] { "RavenDB", "ravendb", "RavenDB6", "ravendb6", AuditRavenDBTypeName })
                {
                    Assert.That(ServiceControlPersisters.GetAuditPersistence(persistenceType).DisplayName, Is.EqualTo("RavenDB 6"), persistenceType);
                }

                Assert.That(ServiceControlPersisters.GetAuditPersistence("RavenDB7").DisplayName, Is.EqualTo("RavenDB 7"));
                Assert.That(ServiceControlPersisters.GetAuditPersistence("ravendb7").DisplayName, Is.EqualTo("RavenDB 7"));
            });
        }

        static IEnumerable<TestCaseData> ManifestOrders()
        {
            yield return new TestCaseData((object)new[] { RavenDB6, RavenDB7 }).SetName("Legacy_first");
            yield return new TestCaseData((object)new[] { RavenDB7, RavenDB6 }).SetName("Modern_first");
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Name_alias_and_type_name_of_the_legacy_persister_find_the_legacy_manifest(PersistenceManifest[] manifests)
        {
            foreach (var persistenceType in new[] { "RavenDB", "ravendb", "RavenDB6", "ravendb6", RavenDBTypeName })
            {
                Assert.That(ServiceControlPersisters.Find(manifests, persistenceType), Is.SameAs(RavenDB6), persistenceType);
            }
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Only_the_RavenDB7_name_finds_the_modern_manifest(PersistenceManifest[] manifests)
        {
            Assert.That(ServiceControlPersisters.Find(manifests, "RavenDB7"), Is.SameAs(RavenDB7));
            Assert.That(ServiceControlPersisters.Find(manifests, "ravendb7"), Is.SameAs(RavenDB7));
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Unknown_persistence_type_finds_no_manifest(PersistenceManifest[] manifests) =>
            Assert.That(ServiceControlPersisters.Find(manifests, "My.fake.persistence, fakePersistenceAssembly"), Is.Null);

        [Test]
        public void Type_name_finds_a_manifest_when_no_name_or_alias_matches()
        {
            var manifest = new PersistenceManifest { Name = "Test", TypeName = "Test.Persistence, Test" };

            Assert.That(ServiceControlPersisters.Find([RavenDB6, manifest], "Test.Persistence, Test"), Is.SameAs(manifest));
        }

        [Test]
        public void Raven35IsDefault()
        {
            var manifests = new[] {
                ServiceControlPersisters.GetPrimaryPersistence(null),
                ServiceControlPersisters.GetAuditPersistence(null),
                ServiceControlPersisters.GetPrimaryPersistence(""),
                ServiceControlPersisters.GetAuditPersistence("")
            };

            Assert.That(manifests.All(m => m.Name == "RavenDB35"));
        }

        const string RavenDBTypeName = "ServiceControl.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Persistence.RavenDB";
        const string AuditRavenDBTypeName = "ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDB";

        static readonly PersistenceManifest RavenDB6 = new()
        {
            Name = "RavenDB",
            DisplayName = "RavenDB 6",
            TypeName = RavenDBTypeName,
            Aliases = ["RavenDB6", RavenDBTypeName]
        };

        static readonly PersistenceManifest RavenDB7 = new()
        {
            Name = "RavenDB7",
            DisplayName = "RavenDB 7",
            TypeName = RavenDBTypeName
        };

        [Test]
        public void UnknownPersister()
        {
            var garbage = "garbage-value";

            var manifests = new[]
            {
                ServiceControlPersisters.GetPrimaryPersistence(garbage),
                ServiceControlPersisters.GetAuditPersistence(garbage)
            };

            Assert.That(manifests.All(m => m.Name == garbage && m.DisplayName == $"Unknown Persistence: {garbage}" && !m.IsSupported));
        }
    }
}
