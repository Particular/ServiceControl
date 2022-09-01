namespace ServiceControl.Audit.Persistence.Tests
{
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.RavenDb;

    class MagicStringTypeTests
    {
        [Test]
        public void Verify_ravendb_persistence_type_string()
        {
            var typeNamespace = DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
            var typeFullName = DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
            var type = typeof(RavenDbPersistenceConfiguration);
            Assert.AreEqual(type.Namespace, typeNamespace);
            Assert.AreEqual(type.FullName, typeFullName);
        }
    }
}
