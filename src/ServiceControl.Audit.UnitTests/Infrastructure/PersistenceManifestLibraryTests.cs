namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

    class PersistenceManifestLibraryTests
    {
        const string persistenceName = "RavenDB5";
        const string persistenceType = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";

        [Test]
        public void Should_find_persistence_type_by_name()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceName);

            Assert.AreEqual(persistenceType, _persistenceType);
        }

        [Test]
        public void Should_find_tpersistence_type_by_type()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceType);

            Assert.AreEqual(persistenceType, _persistenceType);
        }

        [Test]
        public void Should_return_persistence_type_passed_in_if_not_found()
        {
            var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
            var _persistenceType = PersistenceManifestLibrary.Find(fakePersistenceType);

            Assert.AreEqual(fakePersistenceType, _persistenceType);
        }

        [Test]
        public void Should_find_persistence_type_folder_by_name()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceName);

            Assert.AreEqual(persistenceName, _persistenceTypeFolder);
        }

        [Test]
        public void Should_find_tpersistence_type_folder_by_type()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceType);

            Assert.AreEqual(persistenceName, _persistenceTypeFolder);
        }

        [Test]
        public void Should_return_null_for_not_found_persistence_type()
        {
            var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(fakePersistenceType);

            Assert.IsNull(_persistenceTypeFolder);
        }
    }
}