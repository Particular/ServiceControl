namespace ServiceControl.Persistence.Tests.RavenDB.Archiving;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

/// <summary>
/// The operation documents are re-stored after every batch. A restart mid-operation resumes from
/// that persisted state, so it must keep carrying the audit attribution (initiator + operation id)
/// or the per-message audit entries of all remaining batches are silently lost.
/// </summary>
[TestFixture]
class ArchiveOperationAttributionTests : RavenPersistenceTestBase
{
    readonly ProbingDomainEvents events = new();

    public ArchiveOperationAttributionTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test]
    public async Task Archive_operation_keeps_attribution_when_stored_between_batches()
    {
        const string groupId = "TestGroup";

        using (var session = DocumentStore.OpenAsyncSession())
        {
            foreach (var id in new[] { "A", "B", "C", "D" })
            {
                await session.StoreAsync(new FailedMessage
                {
                    Id = "FailedMessages/" + id,
                    UniqueMessageId = id,
                    Status = FailedMessageStatus.Unresolved
                });
            }

            await session.StoreAsync(new ArchiveBatch
            {
                Id = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 0),
                DocumentIds = ["FailedMessages/A", "FailedMessages/B"]
            });
            await session.StoreAsync(new ArchiveBatch
            {
                Id = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1),
                DocumentIds = ["FailedMessages/C", "FailedMessages/D"]
            });

            await session.StoreAsync(new ArchiveOperation
            {
                Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                RequestId = groupId,
                ArchiveType = ArchiveType.FailureGroup,
                TotalNumberOfMessages = 4,
                NumberOfMessagesArchived = 0,
                Started = DateTime.UtcNow,
                GroupName = "Test Group",
                NumberOfBatches = 2,
                CurrentBatch = 0,
                InitiatedById = "alice-sub",
                InitiatedByName = "Alice",
                OperationId = "op-arch"
            });

            await session.SaveChangesAsync();
        }

        // Snapshot the persisted operation right after the first batch is stored — the state a
        // restart would resume from.
        ArchiveOperation stored = null;
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchArchived && stored == null)
            {
                using var session = DocumentStore.OpenAsyncSession();
                stored = await session.LoadAsync<ArchiveOperation>(ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup));
            }
        };

        await ArchiveMessages.ArchiveAllInGroup(groupId);

        Assert.That(stored, Is.Not.Null, "operation document should still exist after the first batch");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.InitiatedById, Is.EqualTo("alice-sub"));
            Assert.That(stored.InitiatedByName, Is.EqualTo("Alice"));
            Assert.That(stored.OperationId, Is.EqualTo("op-arch"));
        }
    }

    [Test]
    public async Task Unarchive_operation_keeps_attribution_when_stored_between_batches()
    {
        const string groupId = "TestGroup";

        using (var session = DocumentStore.OpenAsyncSession())
        {
            foreach (var id in new[] { "A", "B", "C", "D" })
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
                DocumentIds = ["FailedMessages/A", "FailedMessages/B"]
            });
            await session.StoreAsync(new UnarchiveBatch
            {
                Id = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1),
                DocumentIds = ["FailedMessages/C", "FailedMessages/D"]
            });

            await session.StoreAsync(new UnarchiveOperation
            {
                Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                RequestId = groupId,
                ArchiveType = ArchiveType.FailureGroup,
                TotalNumberOfMessages = 4,
                NumberOfMessagesUnarchived = 0,
                Started = DateTime.UtcNow,
                GroupName = "Test Group",
                NumberOfBatches = 2,
                CurrentBatch = 0,
                InitiatedById = "alice-sub",
                InitiatedByName = "Alice",
                OperationId = "op-unarch"
            });

            await session.SaveChangesAsync();
        }

        UnarchiveOperation stored = null;
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchUnarchived && stored == null)
            {
                using var session = DocumentStore.OpenAsyncSession();
                stored = await session.LoadAsync<UnarchiveOperation>(UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup));
            }
        };

        await ArchiveMessages.UnarchiveAllInGroup(groupId);

        Assert.That(stored, Is.Not.Null, "operation document should still exist after the first batch");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.InitiatedById, Is.EqualTo("alice-sub"));
            Assert.That(stored.InitiatedByName, Is.EqualTo("Alice"));
            Assert.That(stored.OperationId, Is.EqualTo("op-unarch"));
        }
    }

    class ProbingDomainEvents : IDomainEvents
    {
        public Func<object, Task> OnRaised { get; set; } = _ => Task.CompletedTask;

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
            => OnRaised(domainEvent);
    }
}
