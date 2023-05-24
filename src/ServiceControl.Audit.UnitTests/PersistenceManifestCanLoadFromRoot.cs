namespace ServiceControl.Audit.UnitTests
{
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

    [TestFixture]
    public class PersistenceManifestCanLoadFromRoot
    {
        const string persistenceName = "RavenDB35";
        const string persistenceType = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb";

        [Test]
        public void Should_find_persistence_type_by_name()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceName);

            Assert.AreEqual(persistenceType, _persistenceType);
        }
    }
}
