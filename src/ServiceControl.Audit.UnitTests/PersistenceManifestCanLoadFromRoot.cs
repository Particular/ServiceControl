namespace ServiceControl.Audit.UnitTests
{
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

    [TestFixture]
    public class PersistenceManifestCanLoadFromRoot
    {
        const string persistenceName = "InMemory";
        const string persistenceType = "ServiceControl.Audit.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Audit.Persistence.InMemory";

        // TODO: Not really sure why this test was here in the first place. Before removing Raven35 code it was testing that "RavenDB35" could be turned
        // into the full persistence type name. But at that time, these UnitTests had references to both the InMemory and Raven35 persistence, and not to
        // Raven5 persistence. So with the removal of Raven35 code, it couldn't work. It has been rewritten to test that the InMemory one can work, since
        // that is the only persister still present in the UnitTests, but I still wonder why it's here as it seems like more of an installer testing concern.

        [Test]
        public void Should_find_persistence_type_by_name()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceName);

            Assert.AreEqual(persistenceType, _persistenceType);
        }
    }
}
