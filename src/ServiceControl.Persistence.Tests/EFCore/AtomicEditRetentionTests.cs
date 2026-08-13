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
/// EF Core-specific retention tests for atomic edit acquisition. These run only against the SQL
/// Server and PostgreSQL persisters and lock in the <see cref="FailedMessageEntity.StatusChangedAt"/>
/// and <see cref="FailedMessageEntity.LastModified"/> behavior used by the retention sweeper.
/// </summary>
class AtomicEditRetentionTests : ErrorIngestionTestBase
{
    IEditFailedMessagesDataStore EditStore => ServiceProvider.GetRequiredService<IEditFailedMessagesDataStore>();

    DateTime Now => PersistenceTestsContext.FakeTime.GetUtcNow().UtcDateTime;

    [Test]
    public async Task TryBeginEdit_stamps_StatusChangedAt_and_LastModified()
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

        var result = await EditStore.TryBeginEdit(failedMessageId, editId);
        Assert.That(result.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));

        var entity = await GetFailedMessage(uniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entity.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            // The resolve must stamp both columns with the current time, not leave the ancient value.
            Assert.That(entity.StatusChangedAt, Is.EqualTo(resolveTime), "StatusChangedAt must be stamped on resolve");
            Assert.That(entity.LastModified, Is.EqualTo(resolveTime), "LastModified must be stamped on resolve");
        }

        Assert.That(await EditStore.GetCurrentEditingRequestId(failedMessageId), Is.EqualTo(editId));
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

        // Atomic edit acquisition re-stamps StatusChangedAt to "now", moving the message
        // back inside the retention window.
        var result = await EditStore.TryBeginEdit(failedMessageId, Guid.NewGuid().ToString());
        Assert.That(result.Outcome, Is.EqualTo(BeginEditOutcome.Acquired));

        Assert.That(await FindFailedMessage(uniqueMessageId), Is.Not.Null, "the just-resolved message must not be swept immediately");

        // Only once the clock advances past the retention period (measured from the edit-set
        // StatusChangedAt) does the sweeper remove it.
        AdvanceClock(TimeSpan.FromDays(31));

        await RunRetentionSweep();

        Assert.That(await FindFailedMessage(uniqueMessageId), Is.Null, "the resolved-via-edit message must be swept after the retention period");
    }

}