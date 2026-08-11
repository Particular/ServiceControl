namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// Focused EFCore-only tests for <see cref="EditFailedMessagesManager"/>. These run only against
/// the SQL Server and PostgreSQL persisters (the RavenDB test project excludes the EFCore test
/// folder). They lock in the <see cref="FailedMessageEntity.StatusChangedAt"/> /
/// <see cref="FailedMessageEntity.LastModified"/> requirement that the retention sweeper relies
/// on, and confirm the edit-id round-trips through the database across manager scopes.
/// </summary>
class EditFailedMessagesManagerTests : ErrorIngestionTestBase
{
    IEditFailedMessagesDataStore EditStore => ServiceProvider.GetRequiredService<IEditFailedMessagesDataStore>();

    DateTime Now => PersistenceTestsContext.FakeTime.GetUtcNow().UtcDateTime;

    [Test]
    public async Task Resolving_via_edit_manager_stamps_StatusChangedAt_and_LastModified()
    {
        // Seed an Unresolved message with an ancient timestamp so that a stale
        // StatusChangedAt would make it immediately sweepable.
        var uniqueMessageId = Guid.NewGuid();
        var ancient = Now.AddDays(-40);
        await Store(new FailedMessageEntity
        {
            UniqueMessageId = uniqueMessageId,
            Status = FailedMessageStatus.Unresolved,
            StatusChangedAt = ancient,
            LastModified = ancient,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = ancient,
            LastTimeOfFailure = ancient,
            LastAttemptedAt = ancient,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyStoredExternally = false,
            BodySize = 0,
            FailingEndpointAddress = "Shipping"
        });

        var failedMessageId = uniqueMessageId.ToString();
        var editId = Guid.NewGuid().ToString();

        var resolveTime = Now;

        await using (var manager = await EditStore.CreateEditFailedMessageManager())
        {
            var failedMessage = await manager.GetFailedMessage(failedMessageId);
            Assert.That(failedMessage, Is.Not.Null);
            Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.Unresolved));

            Assert.That(await manager.GetCurrentEditingRequestId(failedMessageId), Is.Null);

            await manager.SetCurrentEditingRequestId(editId);
            await manager.SetFailedMessageAsResolved();
            await manager.SaveChanges();
        }

        var entity = await GetFailedMessage(uniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            // The resolve must stamp both columns with the current time, not leave the ancient value.
            Assert.That(entity.StatusChangedAt, Is.EqualTo(resolveTime), "StatusChangedAt must be stamped on resolve");
            Assert.That(entity.LastModified, Is.EqualTo(resolveTime), "LastModified must be stamped on resolve");
        }

        // The edit id round-trips through the database across a brand new manager scope (i.e. it is
        // persisted, not held in memory).
        await using var assertionManager = await EditStore.CreateEditFailedMessageManager();
        Assert.That(await assertionManager.GetCurrentEditingRequestId(failedMessageId), Is.EqualTo(editId));
    }

    [Test]
    public async Task Retention_sweeps_a_message_resolved_via_edit_using_the_edit_timestamp()
    {
        EFSettings.ErrorRetentionPeriod = TimeSpan.FromDays(30);

        // Ancient Unresolved message: if the edit left StatusChangedAt untouched, the sweeper
        // would delete it immediately.
        var uniqueMessageId = Guid.NewGuid();
        var ancient = Now.AddDays(-40);
        await Store(new FailedMessageEntity
        {
            UniqueMessageId = uniqueMessageId,
            Status = FailedMessageStatus.Unresolved,
            StatusChangedAt = ancient,
            LastModified = ancient,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = ancient,
            LastTimeOfFailure = ancient,
            LastAttemptedAt = ancient,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyStoredExternally = false,
            BodySize = 0,
            FailingEndpointAddress = "Shipping"
        });

        var failedMessageId = uniqueMessageId.ToString();

        // Resolving via the edit manager re-stamps StatusChangedAt to "now", moving the message
        // back inside the retention window.
        await using (var manager = await EditStore.CreateEditFailedMessageManager())
        {
            await manager.GetFailedMessage(failedMessageId);
            await manager.SetCurrentEditingRequestId(Guid.NewGuid().ToString());
            await manager.SetFailedMessageAsResolved();
            await manager.SaveChanges();
        }

        Assert.That(await FindFailedMessage(uniqueMessageId), Is.Not.Null, "the just-resolved message must not be swept immediately");

        // Only once the clock advances past the retention period (measured from the edit-set
        // StatusChangedAt) does the sweeper remove it.
        AdvanceClock(TimeSpan.FromDays(31));

        await RunRetentionSweep();

        Assert.That(await FindFailedMessage(uniqueMessageId), Is.Null, "the resolved-via-edit message must be swept after the retention period");
    }

    [Test]
    public async Task GetFailedMessage_returns_null_when_not_found()
    {
        await using var manager = await EditStore.CreateEditFailedMessageManager();
        Assert.That(await manager.GetFailedMessage(Guid.NewGuid().ToString()), Is.Null);
    }

    [Test]
    public async Task SetCurrentEditingRequestId_and_SetFailedMessageAsResolved_throw_when_no_message_loaded()
    {
        await using var manager = await EditStore.CreateEditFailedMessageManager();
        Assert.ThrowsAsync<InvalidOperationException>(() => manager.SetCurrentEditingRequestId(Guid.NewGuid().ToString()));
        Assert.ThrowsAsync<InvalidOperationException>(() => manager.SetFailedMessageAsResolved());
    }
}