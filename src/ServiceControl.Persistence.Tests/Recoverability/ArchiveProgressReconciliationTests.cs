namespace ServiceControl.Persistence.Tests.Recoverability;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Recoverability;

/// <summary>
/// Regression tests for the archive/unarchive progress counters shared by every persister:
/// when the batches processed for an operation overran the total planned when it started (live
/// group membership grew mid-operation, or the persisted plan undercounted), the total is widened
/// to the processed count so the reported remaining count never goes negative, both while batches
/// are being counted and when an operation is resumed from persisted state.
/// </summary>
[TestFixture]
class ArchiveProgressReconciliationTests
{
    readonly FakeTimeProvider timeProvider = new();

    [Test]
    public async Task Archive_batches_overrunning_the_total_widen_it_so_remaining_never_goes_negative()
    {
        var archive = NewArchive(total: 1000);

        await archive.Start();
        await archive.BatchArchived(1000);
        await archive.BatchArchived(500); // 500 messages joined the group after the plan was made

        var progress = archive.GetProgress();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress.TotalNumberOfMessages, Is.EqualTo(1500), "the total must adopt the processed count");
            Assert.That(progress.NumberOfMessagesArchived, Is.EqualTo(1500));
            Assert.That(progress.MessagesRemaining, Is.EqualTo(0), "remaining must not go negative");
            Assert.That(progress.Percentage, Is.EqualTo(1.0), "the progress fraction must not exceed 1");
        }
    }

    [Test]
    public async Task Archive_progress_events_report_widened_totals()
    {
        var events = new FakeDomainEvents();
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, events, timeProvider) { TotalNumberOfMessages = 1000 };

        await archive.Start();
        await archive.BatchArchived(1500);

        var batchCompleted = events.RaisedEvents.OfType<ArchiveOperationBatchCompleted>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(batchCompleted.Progress.TotalNumberOfMessages, Is.EqualTo(1500));
            Assert.That(batchCompleted.Progress.NumberOfMessagesArchived, Is.EqualTo(1500));
            Assert.That(batchCompleted.Progress.MessagesRemaining, Is.EqualTo(0));
            Assert.That(batchCompleted.Progress.Percentage, Is.EqualTo(1.0));
        }
    }

    [Test]
    public async Task An_archive_operation_resumed_with_a_processed_count_above_the_total_reconciles_on_start()
    {
        var events = new FakeDomainEvents();
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, events, timeProvider)
        {
            TotalNumberOfMessages = 1000,
            NumberOfMessagesArchived = 1500 // persisted by a plan that undercounted
        };

        await archive.Start();

        var starting = events.RaisedEvents.OfType<ArchiveOperationStarting>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.Progress.TotalNumberOfMessages, Is.EqualTo(1500), "the persisted processed count wins over the stale plan");
            Assert.That(starting.Progress.NumberOfMessagesArchived, Is.EqualTo(1500));
            Assert.That(starting.Progress.MessagesRemaining, Is.EqualTo(0), "a resumed operation must not report a negative remaining count");
        }
    }

    [Test]
    public async Task Unarchive_batches_overrunning_the_total_widen_it_so_remaining_never_goes_negative()
    {
        var unarchive = NewUnarchive(total: 1000);

        await unarchive.Start();
        await unarchive.BatchUnarchived(1000);
        await unarchive.BatchUnarchived(500);

        var progress = unarchive.GetProgress();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress.TotalNumberOfMessages, Is.EqualTo(1500), "the total must adopt the processed count");
            Assert.That(progress.NumberOfMessagesUnarchived, Is.EqualTo(1500));
            Assert.That(progress.MessagesRemaining, Is.EqualTo(0), "remaining must not go negative");
            Assert.That(progress.Percentage, Is.EqualTo(1.0), "the progress fraction must not exceed 1");
        }
    }

    [Test]
    public async Task Unarchive_progress_events_report_widened_totals()
    {
        var events = new FakeDomainEvents();
        var unarchive = new InMemoryUnarchive("group-1", ArchiveType.FailureGroup, events, timeProvider) { TotalNumberOfMessages = 1000 };

        await unarchive.Start();
        await unarchive.BatchUnarchived(1500);

        var batchCompleted = events.RaisedEvents.OfType<UnarchiveOperationBatchCompleted>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(batchCompleted.Progress.TotalNumberOfMessages, Is.EqualTo(1500));
            Assert.That(batchCompleted.Progress.NumberOfMessagesUnarchived, Is.EqualTo(1500));
            Assert.That(batchCompleted.Progress.MessagesRemaining, Is.EqualTo(0));
            Assert.That(batchCompleted.Progress.Percentage, Is.EqualTo(1.0));
        }
    }

    [Test]
    public async Task An_unarchive_operation_resumed_with_a_processed_count_above_the_total_reconciles_on_start()
    {
        var events = new FakeDomainEvents();
        var unarchive = new InMemoryUnarchive("group-1", ArchiveType.FailureGroup, events, timeProvider)
        {
            TotalNumberOfMessages = 1000,
            NumberOfMessagesUnarchived = 1500
        };

        await unarchive.Start();

        var starting = events.RaisedEvents.OfType<UnarchiveOperationStarting>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.Progress.TotalNumberOfMessages, Is.EqualTo(1500));
            Assert.That(starting.Progress.NumberOfMessagesUnarchived, Is.EqualTo(1500));
            Assert.That(starting.Progress.MessagesRemaining, Is.EqualTo(0));
        }
    }

    InMemoryArchive NewArchive(int total) =>
        new("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), timeProvider) { TotalNumberOfMessages = total };

    InMemoryUnarchive NewUnarchive(int total) =>
        new("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), timeProvider) { TotalNumberOfMessages = total };
}