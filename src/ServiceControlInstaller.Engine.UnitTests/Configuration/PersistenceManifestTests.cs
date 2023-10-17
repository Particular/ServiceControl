namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.Linq;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControlInstaller.Engine.Instances;

    public class PersistenceManifestTests
    {
        /*
         * Persistence methods that are no longer supprorted still need to ship a persistence.manifest file so that
         * ServiceControl Management can still display information about older ServiceControl instances.
         */

        [Test]
        public void ApprovePrimaryInstanceManifests()
        {
            Approver.Verify(ServiceControlPersisters.PrimaryPersistenceManifests.Select(m => $"{m.Name}: {m.DisplayName}"));
        }

        [Test]
        public void ApproveAuditInstanceManifests()
        {
            Approver.Verify(ServiceControlPersisters.AuditPersistenceManifests.Select(m => $"{m.Name}: {m.DisplayName}"));
        }
    }
}
