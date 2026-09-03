namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;
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
    public async Task Keeps_the_row_of_a_body_that_cannot_be_deleted()
    {
        var unluckyBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: true);
        RecordedBodies.FailDeleteFor.Add(unluckyBody.ToString());

        using var recorded = ListenToRetentionMetrics();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(unluckyBody), Is.Not.Null, "deleting the row would leave the body with nothing to name it");
            Assert.That(recorded.Cycles(RetentionEntity.FailedMessages).Select(cycle => cycle.Result), Is.EqualTo(new[] { "failed" }));
            Assert.That(recorded.Cycles(RetentionEntity.EventLog).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
        }
    }

    [Test]
    public async Task Retries_a_body_that_could_not_be_deleted_on_the_next_sweep()
    {
        var unluckyBody = await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31), bodyStoredExternally: true);
        RecordedBodies.FailDeleteFor.Add(unluckyBody.ToString());

        await RunRetentionSweep();

        RecordedBodies.FailDeleteFor.Clear();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(unluckyBody), Is.Null);
            Assert.That(RecordedBodies.Deleted, Does.Contain(unluckyBody.ToString()));
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

    [Test]
    public async Task Archived_messages_are_swept_after_the_lifecycle_store_updates_the_timestamp()
    {
        var messageId = await SeedFailedMessage(FailedMessageStatus.Unresolved, Now.AddDays(-40));

        await FailedMessageLifecycleStore.MarkAsArchived(messageId.ToString());

        var archived = await FindFailedMessage(messageId);
        Assert.That(archived, Is.Not.Null);
        Assert.That(archived!.Status, Is.EqualTo(FailedMessageStatus.Archived));
        Assert.That(archived.StatusChangedAt, Is.EqualTo(Now), "the lifecycle store should stamp the current fake time");

        AdvanceClock(TimeSpan.FromDays(31));

        await RunRetentionSweep();

        Assert.That(await FindFailedMessage(messageId), Is.Null);
    }

    [Test]
    public async Task Resolved_messages_are_swept_after_the_lifecycle_store_updates_the_timestamp()
    {
        var messageId = await SeedFailedMessage(FailedMessageStatus.Unresolved, Now.AddDays(-40));

        Assert.That(await FailedMessageLifecycleStore.MarkAsResolved(messageId.ToString()), Is.True);

        var resolved = await FindFailedMessage(messageId);
        Assert.That(resolved, Is.Not.Null);
        Assert.That(resolved!.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        Assert.That(resolved.StatusChangedAt, Is.EqualTo(Now), "the lifecycle store should stamp the current fake time");

        AdvanceClock(TimeSpan.FromDays(31));

        await RunRetentionSweep();

        Assert.That(await FindFailedMessage(messageId), Is.Null);
    }

    [Test]
    public async Task Counts_the_rows_it_deletes()
    {
        EFSettings.EventsRetentionPeriod = TimeSpan.FromDays(14);

        await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-31));
        await SeedFailedMessage(FailedMessageStatus.Resolved, Now.AddDays(-29));
        await Store(EventLogRow("expired", Now.AddDays(-15)));

        var expiredWithGroup = await SeedFailedMessage(FailedMessageStatus.Archived, Now.AddDays(-31));
        await GroupsStore.EditComment(await SeedGroup(expiredWithGroup), "Raised with the shipping team");

        using var recorded = ListenToRetentionMetrics();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.RowsDeleted(RetentionEntity.FailedMessages), Is.EqualTo(2), "the 29 day old message is still within retention");
            Assert.That(recorded.RowsDeleted(RetentionEntity.EventLog), Is.EqualTo(1));
            Assert.That(recorded.RowsDeleted(RetentionEntity.GroupComments), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Records_a_successful_cycle_for_every_pass()
    {
        using var recorded = ListenToRetentionMetrics();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.Cycles(RetentionEntity.FailedMessages).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
            Assert.That(recorded.Cycles(RetentionEntity.EventLog).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
            Assert.That(recorded.Cycles(RetentionEntity.GroupComments).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.Zero);
        }
    }

    [Test]
    public async Task Reports_zero_deleted_rows_when_there_is_nothing_to_sweep()
    {
        using var recorded = ListenToRetentionMetrics();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.Of(RetentionMetrics.RowsDeletedInstrumentName, RetentionEntity.FailedMessages), Is.Not.Empty);
            Assert.That(recorded.Of(RetentionMetrics.RowsDeletedInstrumentName, RetentionEntity.EventLog), Is.Not.Empty);
            Assert.That(recorded.Of(RetentionMetrics.RowsDeletedInstrumentName, RetentionEntity.GroupComments), Is.Not.Empty);
            Assert.That(recorded.RowsDeleted(RetentionEntity.FailedMessages), Is.Zero);
        }
    }

    [Test]
    public async Task A_failing_pass_does_not_stop_the_others()
    {
        EFSettings.EventsRetentionPeriod = TimeSpan.FromDays(14);
        await Store(EventLogRow("expired", Now.AddDays(-15)));

        // Subtracting this from the clock cannot be represented, so the failed messages pass throws
        // before it reaches the database.
        EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(1_000_000);

        using var recorded = ListenToRetentionMetrics();

        await RunRetentionSweep();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.Cycles(RetentionEntity.FailedMessages).Select(cycle => cycle.Result), Is.EqualTo(new[] { "failed" }));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.EqualTo(1));
            Assert.That(recorded.Cycles(RetentionEntity.EventLog).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
            Assert.That(recorded.Cycles(RetentionEntity.GroupComments).Select(cycle => cycle.Result), Is.EqualTo(new[] { "success" }));
            Assert.That(await GetRemainingMarkers(), Does.Not.Contain("expired"));
        }
    }

    RecordedRetentionMetrics ListenToRetentionMetrics() => new(ServiceProvider.GetRequiredService<IMeterFactory>());

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
