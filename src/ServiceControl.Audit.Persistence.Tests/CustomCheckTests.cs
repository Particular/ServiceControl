namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    class CustomCheckTests
    {
        PersistenceDataStoreFixture persistenceDataStoreFixture;

        public CustomCheckTests(PersistenceDataStoreFixture persistenceDataStoreFixture)
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
        public async Task Pass_if_no_failed_imports()
        {
            var customCheck = new FailedAuditImportCustomCheck(persistenceDataStoreFixture.AuditDataStore);

            var result = await customCheck.PerformCheck().ConfigureAwait(false);

            Assert.AreEqual(CheckResult.Pass, result);
        }

        [Test]
        public async Task Fail_if_failed_imports()
        {
            await persistenceDataStoreFixture.AuditDataStore.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);

            await persistenceDataStoreFixture.CompleteDBOperation().ConfigureAwait(false);

            var customCheck = new FailedAuditImportCustomCheck(persistenceDataStoreFixture.AuditDataStore);

            var result = await customCheck.PerformCheck().ConfigureAwait(false);

            Assert.IsTrue(result.HasFailed);
            StringAssert.StartsWith("One or more audit messages have failed to import properly into ServiceControl.Audit and have been stored in the ServiceControl.Audit database.", result.FailureReason);
        }
    }
}