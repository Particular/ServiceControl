namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    class AuditDataStoreTests
    {
        PersistenceDataStoreFixture persistenceDataStoreFixture;

        public AuditDataStoreTests(PersistenceDataStoreFixture persistenceDataStoreFixture)
        {
            this.persistenceDataStoreFixture = persistenceDataStoreFixture;
        }

        [SetUp]
        public async Task Setup()
        {
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