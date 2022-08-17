namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.Tests;

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
    }
}