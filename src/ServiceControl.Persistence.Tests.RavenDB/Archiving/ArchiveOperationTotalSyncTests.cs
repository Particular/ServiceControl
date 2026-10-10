namespace ServiceControl.Persistence.Tests.RavenDB.Archiving;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;

/// <summary>
/// RavenDB iterates a fixed set of pre-built batches while the operation's total comes from a
/// separate group-count index, so the batches can carry more message ids than the planned total.
/// The persisted operation document must adopt the actually processed count when batches overrun
/// the total, so interim progress never reports a negative remaining count and the widened total
/// is what a resumed operation and the completion event see.
/// </summary>
[TestFixture]
class ArchiveOperationTotalSyncTests : RavenPersistenceTestBase
{
    readonly ProbingDomainEvents events = new();

    public ArchiveOperationTotalSyncTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test]
    public async Task Archive_operation_widens_the_persisted_total_when_batches_overrun_it()
    {
        const string groupId = "TestGroup";

        using (var session = DocumentStore.OpenAsyncSession())
        {
            foreach (var id in new[] { "A", "B", "C" })
            {
                await session.StoreAsync(new FailedMessage
                {
                    Id = "FailedMessages/" + id,
                    UniqueMessageId = id,
                    Status = FailedMessageStatus.Unresolved
                });
            }

            // The count index reported 1 message but the batch stream supplies 3 ids.
            await session.StoreAsync(new ArchiveBatch
            {
                Id = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 0),
                DocumentIds = ["FailedMessages/A", "FailedMessages/B", "FailedMessages/C"]
            });

            await session.StoreAsync(new ArchiveOperation
            {
                Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                RequestId = groupId,
                ArchiveType = ArchiveType.FailureGroup,
                TotalNumberOfMessages = 1,
                NumberOfMessagesArchived = 0,
                Started = DateTime.UtcNow,
                GroupName = "Test Group",
                NumberOfBatches = 1,
                CurrentBatch = 0
            });

            await session.SaveChangesAsync();
        }

        ArchiveOperation storedAfterBatch = null;
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchArchived && storedAfterBatch == null)
            {
                using var session = DocumentStore.OpenAsyncSession();
                storedAfterBatch = await session.LoadAsync<ArchiveOperation>(ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup));
            }
        };

        await ArchiveMessages.ArchiveAllInGroup(groupId);

        var progress = events.Raised.OfType<ArchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupArchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress, Has.All.Matches<ArchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress.Last().TotalNumberOfMessages, Is.EqualTo(3), "the planned total must adopt the processed count");
            Assert.That(progress.Last().NumberOfMessagesArchived, Is.EqualTo(3));
            Assert.That(storedAfterBatch, Is.Not.Null, "the operation document should still exist after the batch");
            Assert.That(storedAfterBatch.TotalNumberOfMessages, Is.EqualTo(3), "the persisted total must be synchronized, so a resume starts from the widened total");
            Assert.That(storedAfterBatch.NumberOfMessagesArchived, Is.EqualTo(3));
            Assert.That(completed.MessagesCount, Is.EqualTo(3), "the completion event reports the widened total");
        }
    }

    [Test]
    public async Task Unarchive_operation_widens_the_persisted_total_when_batches_overrun_it()
    {
        const string groupId = "TestGroup";

        using (var session = DocumentStore.OpenAsyncSession())
        {
            foreach (var id in new[] { "A", "B", "C" })
            {
                await session.StoreAsync(new FailedMessage
                {
                    Id = "FailedMessages/" + id,
                    UniqueMessageId = id,
                    Status = FailedMessageStatus.Archived
                });
            }

            await session.StoreAsync(new UnarchiveBatch
            {
                Id = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 0),
                DocumentIds = ["FailedMessages/A", "FailedMessages/B", "FailedMessages/C"]
            });

            await session.StoreAsync(new UnarchiveOperation
            {
                Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                RequestId = groupId,
                ArchiveType = ArchiveType.FailureGroup,
                TotalNumberOfMessages = 1,
                NumberOfMessagesUnarchived = 0,
                Started = DateTime.UtcNow,
                GroupName = "Test Group",
                NumberOfBatches = 1,
                CurrentBatch = 0
            });

            await session.SaveChangesAsync();
        }

        UnarchiveOperation storedAfterBatch = null;
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchUnarchived && storedAfterBatch == null)
            {
                using var session = DocumentStore.OpenAsyncSession();
                storedAfterBatch = await session.LoadAsync<UnarchiveOperation>(UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup));
            }
        };

        await ArchiveMessages.UnarchiveAllInGroup(groupId);

        var progress = events.Raised.OfType<UnarchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupUnarchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(progress, Has.All.Matches<UnarchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress.Last().TotalNumberOfMessages, Is.EqualTo(3), "the planned total must adopt the processed count");
            Assert.That(progress.Last().NumberOfMessagesUnarchived, Is.EqualTo(3));
            Assert.That(storedAfterBatch, Is.Not.Null, "the operation document should still exist after the batch");
            Assert.That(storedAfterBatch.TotalNumberOfMessages, Is.EqualTo(3), "the persisted total must be synchronized, so a resume starts from the widened total");
            Assert.That(storedAfterBatch.NumberOfMessagesUnarchived, Is.EqualTo(3));
            Assert.That(completed.MessagesCount, Is.EqualTo(3), "the completion event reports the widened total");
        }
    }

    [Test]
    public async Task Archive_operation_resumed_with_a_processed_count_above_the_total_reports_non_negative_progress()
    {
        const string groupId = "TestGroup";

        using (var session = DocumentStore.OpenAsyncSession())
        {
            await session.StoreAsync(new FailedMessage
            {
                Id = "FailedMessages/A",
                UniqueMessageId = "A",
                Status = FailedMessageStatus.Unresolved
            });

            await session.StoreAsync(new ArchiveBatch
            {
                Id = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 0),
                DocumentIds = ["FailedMessages/A"]
            });

            // Persisted by a version that did not reconcile the plan: 2 processed of 1 planned.
            await session.StoreAsync(new ArchiveOperation
            {
                Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                RequestId = groupId,
                ArchiveType = ArchiveType.FailureGroup,
                TotalNumberOfMessages = 1,
                NumberOfMessagesArchived = 2,
                Started = DateTime.UtcNow,
                GroupName = "Test Group",
                NumberOfBatches = 1,
                CurrentBatch = 0
            });

            await session.SaveChangesAsync();
        }

        await ArchiveMessages.ArchiveAllInGroup(groupId);

        var starting = events.Raised.OfType<ArchiveOperationStarting>().Single();
        var progress = events.Raised.OfType<ArchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupArchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.Progress.MessagesRemaining, Is.EqualTo(0), "a resumed operation must not report a negative remaining count");
            Assert.That(starting.Progress.TotalNumberOfMessages, Is.EqualTo(2), "the persisted processed count wins over the stale plan");
            Assert.That(progress, Has.All.Matches<ArchiveProgress>(p => p.MessagesRemaining >= 0));
            Assert.That(completed.MessagesCount, Is.EqualTo(3), "completion reports the processed count of the resumed run");
        }
    }

    class ProbingDomainEvents : IDomainEvents
    {
        public Func<object, Task> OnRaised { get; set; } = _ => Task.CompletedTask;

        public System.Collections.Generic.List<object> Raised { get; } = [];

        public async Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            Raised.Add(domainEvent);
            await OnRaised(domainEvent);
        }
    }
}