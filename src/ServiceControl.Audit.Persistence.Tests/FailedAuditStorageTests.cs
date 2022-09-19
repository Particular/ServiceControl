namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;

    [TestFixture]
    class FailedAuditStorageTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_store_failures()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);

            await configuration.CompleteDBOperation().ConfigureAwait(false);

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);

            Assert.AreEqual(1, numFailures);
        }

        [Test]
        public async Task Should_be_able_to_process_failures()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport()).ConfigureAwait(false);

            await configuration.CompleteDBOperation().ConfigureAwait(false);

            var succeeded = 0;
            await FailedAuditStorage.ProcessFailedMessages(async (transportMessage, markComplete, token) =>
            {
                await markComplete(token)
                    .ConfigureAwait(false);
                succeeded++;
            }, CancellationToken.None).ConfigureAwait(false);

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount().ConfigureAwait(false);

            Assert.AreEqual(2, succeeded);
            Assert.AreEqual(0, numFailures);
        }
    }
}