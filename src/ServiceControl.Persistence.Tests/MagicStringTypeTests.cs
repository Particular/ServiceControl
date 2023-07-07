﻿namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.RavenDb;

    class MagicStringTypeTests
    {
        [Test]
        public Task Verify_ravendb_persistence_type_string()
        {
            var typeNamespace = DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName.Split(',')[1].Trim();
            var typeFullName = DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName.Split(',')[0].Trim();
            var type = typeof(RavenDbPersistenceConfiguration);
            Assert.AreEqual(type.Namespace, typeNamespace);
            Assert.AreEqual(type.FullName, typeFullName);
            return Task.CompletedTask;
        }
    }
}