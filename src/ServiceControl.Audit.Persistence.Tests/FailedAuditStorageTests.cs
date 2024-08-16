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
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount();

            Assert.That(numFailures, Is.EqualTo(1));
        }

        [Test]
        public async Task Should_be_able_to_process_failures()
        {
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());
            await FailedAuditStorage.SaveFailedAuditImport(new FailedAuditImport());

            await configuration.CompleteDBOperation();

            var succeeded = 0;
            await FailedAuditStorage.ProcessFailedMessages(async (transportMessage, markComplete, token) =>
            {
                await markComplete(token);
                succeeded++;
            }, CancellationToken.None);

            await configuration.CompleteDBOperation();

            var numFailures = await FailedAuditStorage.GetFailedAuditsCount();

            Assert.That(succeeded, Is.EqualTo(2));
            Assert.That(numFailures, Is.EqualTo(0));
        }
    }
}