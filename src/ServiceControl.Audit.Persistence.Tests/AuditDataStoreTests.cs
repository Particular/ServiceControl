namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class AuditDataStoreTests : PersistenceTestFixture
    {
        [Test]
        public async Task DataStore_should_setup_ok()
        {
            var sagaHistory = await DataStore.QuerySagaHistoryById(Guid.NewGuid()).ConfigureAwait(false);

            Assert.IsTrue(sagaHistory.Results == null);
        }
    }
}