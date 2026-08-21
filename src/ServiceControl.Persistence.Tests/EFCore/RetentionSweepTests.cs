namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;

class RetentionSweepTests : ErrorIngestionTestBase
{
    [SetUp]
    public void SetRetention() => EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(30);

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

    [Test]
    public async Task Deletes_comments_of_groups_that_no_longer_have_messages()
    {
        var expired = await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-31));
        var groupId = await SeedGroup(expired);

        await GroupsStore.EditComment(groupId, "Raised with the shipping team");

        await RunRetentionSweep();

        Assert.That(await FindGroupComment(groupId), Is.Null);
    }

    [Test]
    public async Task Keeps_comments_of_groups_that_still_have_messages()
    {
        var live = await SeedFailedMessage(FailedMessageStatus.Unresolved, Now);
        var groupId = await SeedGroup(live);

        await GroupsStore.EditComment(groupId, "Raised with the shipping team");

        await RunRetentionSweep();

        Assert.That(await FindGroupComment(groupId), Is.Not.Null);
    }

    [Test]
    public async Task Archived_messages_are_swept_after_the_archiver_updates_the_timestamp()
    {
        var groupId = Guid.NewGuid().ToString();
        var messageId = await SeedFailedMessage(FailedMessageStatus.Unresolved, Now.AddDays(-40));
        await Store(new FailedMessageGroupEntity { FailedMessageUniqueId = messageId, GroupId = groupId, Title = "t", Type = "Message Type" });

        await ArchiveMessages.ArchiveAllInGroup(groupId);

        var archived = await FindFailedMessage(messageId);
        Assert.That(archived, Is.Not.Null);
        Assert.That(archived!.Status, Is.EqualTo(FailedMessageStatus.Archived));
        Assert.That(archived.StatusChangedAt, Is.EqualTo(Now), "the archiver should stamp the current fake time");

        // The archiver reset the timestamp to Now, so the message is back inside the retention window.
        // Only after the clock advances past the retention period can the sweeper remove it.
        AdvanceClock(TimeSpan.FromDays(31));

        await RunRetentionSweep();

        Assert.That(await FindFailedMessage(messageId), Is.Null);
    }

    async Task<string> SeedGroup(Guid uniqueMessageId)
    {
        var groupId = Guid.NewGuid().ToString();

        await Store(new FailedMessageGroupEntity
        {
            FailedMessageUniqueId = uniqueMessageId,
            GroupId = groupId,
            Title = "ShippingFailed",
            Type = "Message Type"
        });

        return groupId;
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

    [Test]
    public async Task Deletes_event_log_items_past_the_events_cutoff()
    {
        EFSettings.EventsRetentionPeriod = TimeSpan.FromDays(14);

        await Store(EventLogRow("expired", Now.AddDays(-15)));
        await Store(EventLogRow("fresh", Now.AddDays(-13)));

        await RunRetentionSweep();

        var remaining = await GetRemainingMarkers();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(remaining, Does.Not.Contain("expired"));
            Assert.That(remaining, Does.Contain("fresh"));
        }
    }

    [Test]
    public async Task Event_log_retention_is_independent()
    {
        // A 30 day error retention must not keep a 1 day event log item alive.
        EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(30);
        EFSettings.EventsRetentionPeriod = TimeSpan.FromDays(1);

        await Store(EventLogRow("old-event", Now.AddDays(-2)));

        await RunRetentionSweep();

        Assert.That(await GetRemainingMarkers(), Does.Not.Contain("old-event"));
    }

    [Test]
    public async Task Sweeping_event_log_items_changes_the_version()
    {
        EFSettings.EventsRetentionPeriod = TimeSpan.FromDays(14);

        await Store(EventLogRow("expired", Now.AddDays(-15)));
        await Store(EventLogRow("fresh", Now.AddDays(-1)));

        var versionBefore = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).QueryStats.Version;

        await RunRetentionSweep();

        var after = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.QueryStats.TotalCount, Is.EqualTo(1));
            // The count term of the version exists precisely so that retention invalidates client caches.
            Assert.That(after.QueryStats.Version.Matches(versionBefore), Is.False);
        }
    }

    [Test]
    public async Task Sweeping_failed_messages_changes_the_version()
    {
        await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-31));
        await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-1));

        var versionBefore = (await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null)).Version;

        await RunRetentionSweep();

        var after = await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.TotalCount, Is.EqualTo(1));
            Assert.That(after.Version.Matches(versionBefore), Is.False);
        }
    }

    static EventLogItemEntity EventLogRow(string marker, DateTime raisedAt) => new()
    {
        Description = marker,
        Severity = Severity.Info,
        RaisedAt = raisedAt,
        RelatedTo = [],
        Category = "Recoverability",
        EventType = "MessageFailed"
    };

    async Task<List<string>> GetRemainingMarkers()
    {
        var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 100))).Results;
        return [.. items.Select(i => i.Description)];
    }
}
