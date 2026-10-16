namespace ServiceControl.Persistence.Tests.RavenDB
{
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Persistence.RavenDB;
    using TestHelper;

    [TestFixture]
    class IndexDefinitionApprovals
    {
        // A changed definition makes RavenDB rebuild the index side by side at the next start-up, which can take days.
        // Record the change in the upgrade guide before approving it, see docs/testing.md.
        [Test]
        public void Verify_index_definitions_are_unchanged() =>
            Approver.Verify(IndexDefinitionText.Render(typeof(DatabaseSetup).Assembly));
    }
}
