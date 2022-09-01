namespace ServiceControl.Audit.Persistence.Tests
{
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.InMemory;

    class MagicStringTypeTests
    {
        [Test]
        public void Verify_inmemory_persistence_type_string()
        {
            var typeNamespace = DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
            var typeFullName = DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
            var type = typeof(InMemoryPersistenceConfiguration);
            Assert.AreEqual(type.Namespace, typeNamespace);
            Assert.AreEqual(type.FullName, typeFullName);
        }

        //TODO: move to new SQL test project
        //[Test]
        //public Task Verify_sqlserver_persistence_type_string()
        //{
        //    var typeNamespace = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
        //    var typeFullName = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
        //    var type = typeof(SqlDbPersistenceConfiguration);
        //    Assert.AreEqual(type.Namespace, typeNamespace);
        //    Assert.AreEqual(type.FullName, typeFullName);
        //    return Task.CompletedTask;
        //}
    }
}
