namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class FailedMessageWriteVersionTests : IngestionTestBase
{
    [Test]
    public Task Resolving_one_moves_the_version() =>
        AssertVersionMoves(failure => FailedMessageLifecycleStore.MarkAsResolved(failure.UniqueMessageIdString));

    [Test]
    public Task Recording_a_successful_retry_moves_the_version() =>
        AssertVersionMoves(failure => ConfirmRetry(failure.UniqueMessageIdString));

    [Test]
    public Task Archiving_moves_the_version() =>
        AssertVersionMoves(failure => FailedMessageLifecycleStore.MarkAsArchived(failure.UniqueMessageIdString));

    [Test]
    public Task Beginning_an_edit_moves_the_version() =>
        AssertVersionMoves(failure => EditFailedMessagesStore.TryBeginEdit(failure.UniqueMessageIdString, "edit-request-1"));

    [Test]
    public async Task Unarchiving_moves_the_version()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await FailedMessageLifecycleStore.MarkAsArchived(failure.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var before = await AllErrors();

        AdvanceClock(TimeSpan.FromMinutes(1));

        await FailedMessageLifecycleStore.UnArchiveMessages([failure.UniqueMessageIdString]);
        await CompleteDatabaseOperation();

        VersionAssert.Moved(before.QueryStats.Version, (await AllErrors()).QueryStats.Version,
            "the message is unresolved again, so a client holding the archived view must not be told it is current");
    }

    [Test]
    public async Task A_further_attempt_moves_the_version_without_moving_the_count()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var before = await AllErrors();

        AdvanceClock(TimeSpan.FromMinutes(5));

        await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(5)));
        await CompleteDatabaseOperation();

        var after = await AllErrors();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.QueryStats.TotalCount, Is.EqualTo(before.QueryStats.TotalCount),
                "the setup only bites while the count is unchanged, so the count cannot be what moves the version");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the row reports another attempt, so the validator has to move with it");
        }
    }

    async Task AssertVersionMoves(Func<IngestedFailure, Task> write)
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var before = await AllErrors();

        AdvanceClock(TimeSpan.FromMinutes(1));

        await write(failure);
        await CompleteDatabaseOperation();

        VersionAssert.Moved(before.QueryStats.Version, (await AllErrors()).QueryStats.Version,
            "this write changes what the response reports, so a revalidating client must not be told its page is current");
    }

    Task<QueryResult<IList<FailedMessageView>>> AllErrors() =>
        FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(), new SortInfo());
}
