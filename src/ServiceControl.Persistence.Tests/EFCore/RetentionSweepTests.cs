namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

class RetentionSweepTests : ErrorIngestionTestBase
{
    [SetUp]
    public void SetRetention() => EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(30);

    DateTime Now => PersistenceTestsContext.FakeTime.GetUtcNow().UtcDateTime;

    [Test]
    public async Task Deletes_resolved_and_archived_rows_past_the_cutoff()
    {
        var oldResolved = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31));
        var oldArchived = await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-31));

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(oldResolved), Is.Null);
            Assert.That(await FindFailedMessage(oldArchived), Is.Null);
        }
    }

    [Test]
    public async Task Keeps_resolved_and_archived_rows_within_the_cutoff()
    {
        var recentResolved = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-29));
        var recentArchived = await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-29));

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(recentResolved), Is.Not.Null);
            Assert.That(await FindFailedMessage(recentArchived), Is.Not.Null);
        }
    }

    [Test]
    public async Task Never_deletes_unresolved_or_retry_issued_rows_however_old()
    {
        var ancientUnresolved = await SeedFailedMessage(FailedMessageStatus.Unresolved, Now.AddYears(-5));
        var ancientRetryIssued = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Now.AddYears(-5));

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(ancientUnresolved), Is.Not.Null);
            Assert.That(await FindFailedMessage(ancientRetryIssued), Is.Not.Null);
        }
    }

    [Test]
    public async Task Shrinking_the_retention_takes_effect_on_the_next_run()
    {
        var message = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-20));

        await RunRetentionSweep();
        Assert.That(await FindFailedMessage(message), Is.Not.Null, "20 days old is still within the 30 day retention");

        // No row rewrite, only the setting changes; the next run recomputes the cutoff.
        EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(10);

        await RunRetentionSweep();
        Assert.That(await FindFailedMessage(message), Is.Null, "now past the shrunk 10 day retention");
    }

    [Test]
    public async Task Deletes_the_external_bodies_of_swept_rows_only()
    {
        var externalBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: true);
        var inlineBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: false);

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RecordedBodies.Deleted, Does.Contain(externalBody.ToString()), "the external body must be deleted");
            Assert.That(RecordedBodies.Deleted, Does.Not.Contain(inlineBody.ToString()), "an inline body needs no external cleanup");
        }
    }

    [Test]
    public async Task Also_removes_the_group_rows_of_swept_messages()
    {
        var message = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31));
        await Store(new FailedMessageGroupEntity { FailedMessageUniqueId = message, GroupId = "group-1", Title = "t", Type = "Message Type" });

        await RunRetentionSweep();

        Assert.That(await GetGroups(message), Is.Empty, "the cascade must remove the group rows");
    }

    [Test]
    public async Task Tolerates_a_body_that_cannot_be_deleted()
    {
        var unluckyBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: true);
        var otherBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: true);
        RecordedBodies.FailDeleteFor.Add(unluckyBody.ToString());

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            // The failed delete must not stall retention: both rows are still swept.
            Assert.That(await FindFailedMessage(unluckyBody), Is.Null);
            Assert.That(await FindFailedMessage(otherBody), Is.Null);
            Assert.That(RecordedBodies.Deleted, Does.Contain(otherBody.ToString()));
        }
    }

    async Task<Guid> SeedFailedMessage(FailedMessageStatus status, DateTime statusChangedAt, bool bodyStoredExternally = false)
    {
        var id = Guid.NewGuid();

        await Store(new FailedMessageEntity
        {
            UniqueMessageId = id,
            Status = status,
            StatusChangedAt = statusChangedAt,
            LastModified = statusChangedAt,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = statusChangedAt,
            LastTimeOfFailure = statusChangedAt,
            LastAttemptedAt = statusChangedAt,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyStoredExternally = bodyStoredExternally,
            BodySize = 0,
            FailingEndpointAddress = "Shipping"
        });

        return id;
    }
}
