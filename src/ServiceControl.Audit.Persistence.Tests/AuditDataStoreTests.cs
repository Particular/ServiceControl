namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class AuditDataStoreTests
    {
        PersistenceTestFixture persistenceDataStoreFixture;

        [SetUp]
        public async Task Setup()
        {
            var c = new TestSuiteConstraints();
            persistenceDataStoreFixture = c.CreatePersistenceTestFixture();

            await persistenceDataStoreFixture.SetupDataStore().ConfigureAwait(false);
        }

        [TearDown]
        public async Task Cleanup()
        {
            await persistenceDataStoreFixture.CleanupDB().ConfigureAwait(false);
        }

        [Test]
        public async Task DataStore_should_setup_ok()
        {
            var sagaHistory = await persistenceDataStoreFixture.AuditDataStore.QuerySagaHistoryById(Guid.NewGuid()).ConfigureAwait(false);

            Assert.IsTrue(sagaHistory.Results == null);
        }
    }
}