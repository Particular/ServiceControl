namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

class FailedMessageLifecycleDataStoreTests : ErrorIngestionTestBase
{
    // Advancing the clock wakes the live sweeper, so retention has to outrun every advance below.
    [SetUp]
    public void SetRetention() => EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(365);

    [Test]
    public async Task Marking_as_archived_stamps_the_injected_clock()
    {
        var messageId = await Seed(FailedMessageStatus.Unresolved);

        AdvanceClock(TimeSpan.FromDays(7));

        await FailedMessageLifecycleStore.MarkAsArchived(messageId.ToString());

        await AssertStampedWithCurrentTime(messageId, FailedMessageStatus.Archived);
    }

    [Test]
    public async Task Marking_as_resolved_stamps_the_injected_clock()
    {
        var messageId = await Seed(FailedMessageStatus.Unresolved);

        AdvanceClock(TimeSpan.FromDays(7));

        Assert.That(await FailedMessageLifecycleStore.MarkAsResolved(messageId.ToString()), Is.True);

        await AssertStampedWithCurrentTime(messageId, FailedMessageStatus.Resolved);
    }

    [Test]
    public async Task Unarchiving_stamps_the_injected_clock()
    {
        var messageId = await Seed(FailedMessageStatus.Archived);

        AdvanceClock(TimeSpan.FromDays(7));

        var unarchived = await FailedMessageLifecycleStore.UnArchiveMessages([messageId.ToString()]);
        Assert.That(unarchived, Is.EquivalentTo(new[] { messageId.ToString() }));

        await AssertStampedWithCurrentTime(messageId, FailedMessageStatus.Unresolved);
    }

    [Test]
    public async Task Unarchiving_by_range_stamps_the_injected_clock()
    {
        var archivedAt = Now;
        var messageId = await Seed(FailedMessageStatus.Archived, archivedAt);

        AdvanceClock(TimeSpan.FromDays(7));

        var unarchived = await FailedMessageLifecycleStore.UnArchiveMessagesByRange(archivedAt.AddDays(-1), archivedAt.AddDays(1));
        Assert.That(unarchived, Is.EquivalentTo(new[] { messageId.ToString() }));

        await AssertStampedWithCurrentTime(messageId, FailedMessageStatus.Unresolved);
    }

    [Test]
    public async Task Reverting_a_retry_stamps_the_injected_clock()
    {
        var messageId = await Seed(FailedMessageStatus.RetryIssued);

        AdvanceClock(TimeSpan.FromDays(7));

        await FailedMessageLifecycleStore.RevertRetry(messageId.ToString());

        await AssertStampedWithCurrentTime(messageId, FailedMessageStatus.Unresolved);
    }

    async Task AssertStampedWithCurrentTime(Guid messageId, FailedMessageStatus expectedStatus)
    {
        var row = await GetFailedMessage(messageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(expectedStatus));
            Assert.That(row.StatusChangedAt, Is.EqualTo(Now));
            Assert.That(row.LastModified, Is.EqualTo(Now));
        }
    }

    async Task<Guid> Seed(FailedMessageStatus status, DateTime? statusChangedAt = null)
    {
        var id = Guid.NewGuid();
        var timestamp = statusChangedAt ?? Now;

        await Store(new FailedMessageEntity
        {
            UniqueMessageId = id,
            Status = status,
            StatusChangedAt = timestamp,
            LastModified = timestamp,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = timestamp,
            LastTimeOfFailure = timestamp,
            LastAttemptedAt = timestamp,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyStoredExternally = false,
            BodySize = 0,
            FailingEndpointAddress = "Shipping"
        });

        return id;
    }
}
