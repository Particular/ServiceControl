namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
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