namespace ServiceControl.Audit.Persistence.Tests
{
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure.Settings;
    using ServiceControl.Audit.Persistence.SqlServer;

    class MagicStringTypeTests
    {
        [Test]
        public void Verify_sqlserver_persistence_type_string()
        {
            var typeNamespace = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
            var typeFullName = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
            var type = typeof(SqlDbPersistenceConfiguration);
            Assert.AreEqual(type.Namespace, typeNamespace);
            Assert.AreEqual(type.FullName, typeFullName);
        }
    }
}
