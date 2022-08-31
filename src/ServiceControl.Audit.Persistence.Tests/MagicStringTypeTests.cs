//namespace ServiceControl.Audit.Persistence.Tests
//{
//    using System.Threading.Tasks;
//    using NUnit.Framework;
//    using ServiceControl.Audit.Infrastructure.Settings;
//    using ServiceControl.Audit.Persistence.InMemory;
//    using ServiceControl.Audit.Persistence.RavenDb;
//    using ServiceControl.Audit.Persistence.SqlServer;

//    class MagicStringTypeTests
//    {
//        [Test]
//        public Task Verify_inmemory_persistence_type_string()
//        {
//            var typeNamespace = DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
//            var typeFullName = DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
//            var type = typeof(InMemoryPersistenceConfiguration);
//            Assert.AreEqual(type.Namespace, typeNamespace);
//            Assert.AreEqual(type.FullName, typeFullName);
//            return Task.CompletedTask;
//        }

//        [Test]
//        public Task Verify_sqlserver_persistence_type_string()
//        {
//            var typeNamespace = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
//            var typeFullName = DataStoreConfig.SqlServerPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
//            var type = typeof(SqlDbPersistenceConfiguration);
//            Assert.AreEqual(type.Namespace, typeNamespace);
//            Assert.AreEqual(type.FullName, typeFullName);
//            return Task.CompletedTask;
//        }

//        [Test]
//        public Task Verify_ravendb_persistence_type_string()
//        {
//            var typeNamespace = DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
//            var typeFullName = DataStoreConfig.RavenDbPersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
//            var type = typeof(RavenDbPersistenceConfiguration);
//            Assert.AreEqual(type.Namespace, typeNamespace);
//            Assert.AreEqual(type.FullName, typeFullName);
//            return Task.CompletedTask;
//        }
//    }
//}
