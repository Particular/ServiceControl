namespace ServiceControl.UnitTests.Migration;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class FinishedCopyBeforeAnIngestionNodeOpensTests
{
    [Test]
    public async Task A_category_blocked_behind_a_failed_one_is_named_as_waiting_for_it()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages, MigrationCategoryState.CompleteWithErrors, "cursor-33", 18, 15, null, null, null, null, null, null));
        await store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.GroupComments, MigrationCategoryState.Blocked, null, 0, 0, null, null, null, null, null, null));

        var exception = Assert.ThrowsAsync<Exception>(() => new FinishedCopyBeforeAnIngestionNodeOpens(store, "ServiceControl").StartingAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.Contain($"{MigrationCategoryIds.GroupComments} is waiting for {MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages} (Blocked)"));
            Assert.That(exception.Message, Does.Not.Contain($"{MigrationCategoryIds.GroupComments} is Copying"));
        }
    }
}
