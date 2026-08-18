namespace ServiceControl.Persistence.Tests.Recoverability;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.MessageFailures;
using ServiceControl.Recoverability;

[TestFixture]
class ArchiveCancellationTests : PersistenceTestBase
{
    const string Classifier = "Exception Type and Stack Trace";

    static readonly DateTime Noon = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    readonly CancelOnFirstBatch events = new();

    public ArchiveCancellationTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test, CancelAfter(60_000)]
    public async Task Archiving_completes_when_a_cancelled_run_is_retried()
    {
        var group = NewGroup();
        await Insert(InGroup(group), InGroup(group), InGroup(group));

        Assert.That(
            async () => await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: events.Token),
            Throws.InstanceOf<OperationCanceledException>(),
            "cancelling mid-operation should surface rather than be swallowed");

        await AssertNoUnresolvedMessagesIn(group);

        await ArchiveMessages.ArchiveAllInGroup(group.Id);

        await AssertNoUnresolvedMessagesIn(group);
    }

    [Test, CancelAfter(60_000)]
    public async Task Cancelled_archiving_leaves_the_batch_it_had_already_committed()
    {
        var group = NewGroup();
        await Insert(InGroup(group), InGroup(group));

        Assert.That(
            async () => await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: events.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(events.BatchesArchived, Is.EqualTo(1), "exactly one batch should have been archived before cancelling");

        await AssertNoUnresolvedMessagesIn(group);
    }

    async Task AssertNoUnresolvedMessagesIn(FailedMessage.FailureGroup group)
    {
        await CompleteDatabaseOperation();

        var groups = await GroupsStore.GetUnresolvedGroupsByClassifier(Classifier, null);

        Assert.That(
            groups.Select(view => view.Id),
            Does.Not.Contain(group.Id),
            "every message in the group should have left the Unresolved status");
    }

    static FailedMessage.FailureGroup NewGroup() =>
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = Classifier };

    static IngestedFailure InGroup(FailedMessage.FailureGroup group) =>
        new()
        {
            Groups = [group],
            AttemptedAt = Noon,
            TimeOfFailure = Noon,
            TimeSent = Noon.AddMinutes(-1)
        };

    async Task Insert(params IngestedFailure[] failures)
    {
        var messages = failures.Select(failure => failure.ToFailedMessage()).ToArray();

        foreach (var message in messages)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(messages);
        await CompleteDatabaseOperation();
    }

    /// <summary>
    /// Cancels as soon as a batch has been archived. Both persisters raise
    /// <see cref="FailedMessageGroupBatchArchived" /> only after the batch and its progress have been
    /// committed, so this interrupts the operation at a batch boundary rather than mid-write.
    /// Cancellation is honoured on the way in, the way the real implementation does, so the token is
    /// observed on the next event the archiver raises.
    /// </summary>
    sealed class CancelOnFirstBatch : IDomainEvents
    {
        readonly CancellationTokenSource source = new();

        public CancellationToken Token => source.Token;

        public int BatchesArchived { get; private set; }

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (domainEvent is FailedMessageGroupBatchArchived)
            {
                BatchesArchived++;
                source.Cancel();
            }

            return Task.CompletedTask;
        }
    }
}
