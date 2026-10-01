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

/// <summary>
/// Failure-group archive/unarchive must stay within the budget planned when the operation started
/// even if new failures keep joining the group while it runs, and its reported progress must never
/// go negative. Each batch completion injects new failures into the group, simulating live
/// arrivals: an unbounded batch loop (or a remaining count computed from a stale plan) makes these
/// assertions fail. Members that join late are accepted as leftovers for a later operation.
/// </summary>
[TestFixture]
class ArchiveGroupArrivalsTests : PersistenceTestBase
{
    const int BatchSize = 1000; // must match the persisters' archive batch size
    const int MaxArrivalInjections = 10; // bounds the run when the loop is unbounded

    static readonly DateTime Noon = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    readonly ArrivalsDomainEvents events = new();

    public ArchiveGroupArrivalsTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test]
    [CancelAfter(180_000)]
    public async Task Archive_group_stays_within_its_planned_budget_when_failures_arrive_mid_operation(CancellationToken cancellationToken = default)
    {
        var group = NewGroup();
        await Insert(group, 2 * BatchSize, FailedMessageStatus.Unresolved);

        events.OnRaised = domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchArchived && events.ArrivalInjections < MaxArrivalInjections)
            {
                events.ArrivalInjections++;
                return Insert(group, BatchSize, FailedMessageStatus.Unresolved);
            }

            return Task.CompletedTask;
        };

        await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: cancellationToken);

        await CompleteDatabaseOperation();

        var batchEvents = events.Raised.OfType<FailedMessageGroupBatchArchived>().ToArray();
        var progress = events.Raised.OfType<ArchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupArchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batchEvents, Has.Length.EqualTo(2), "the planned batch count (2 x 1000) is a hard cap");
            Assert.That(batchEvents.Select(e => e.FailedMessagesIds.Length), Is.EqualTo(new[] { BatchSize, BatchSize }));
            Assert.That(progress, Has.All.Matches<ArchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress, Has.All.Matches<ArchiveProgress>(p => p.TotalNumberOfMessages >= p.NumberOfMessagesArchived));
            Assert.That(completed.MessagesCount, Is.EqualTo(2 * BatchSize), "the completed event reports the planned total");
        }

        var leftover = await GroupsStore.GetUnresolvedGroup(group.Id, null, null, cancellationToken);
        Assert.That(leftover.Results?.Count, Is.EqualTo(2 * BatchSize),
            "members that joined late are left for a later operation, and the loop must not run past its budget");
    }

    [Test]
    [CancelAfter(180_000)]
    public async Task Unarchive_group_stays_within_its_planned_budget_when_failures_arrive_mid_operation(CancellationToken cancellationToken = default)
    {
        var group = NewGroup();
        await Insert(group, 2 * BatchSize, FailedMessageStatus.Archived);

        events.OnRaised = domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchUnarchived && events.ArrivalInjections < MaxArrivalInjections)
            {
                events.ArrivalInjections++;
                return Insert(group, BatchSize, FailedMessageStatus.Archived);
            }

            return Task.CompletedTask;
        };

        await ArchiveMessages.UnarchiveAllInGroup(group.Id, cancellationToken: cancellationToken);

        await CompleteDatabaseOperation();

        var batchEvents = events.Raised.OfType<FailedMessageGroupBatchUnarchived>().ToArray();
        var progress = events.Raised.OfType<UnarchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupUnarchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batchEvents, Has.Length.EqualTo(2), "the planned batch count (2 x 1000) is a hard cap");
            Assert.That(batchEvents.Select(e => e.FailedMessagesIds.Length), Is.EqualTo(new[] { BatchSize, BatchSize }));
            Assert.That(progress, Has.All.Matches<UnarchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress, Has.All.Matches<UnarchiveProgress>(p => p.TotalNumberOfMessages >= p.NumberOfMessagesUnarchived));
            Assert.That(completed.MessagesCount, Is.EqualTo(2 * BatchSize), "the completed event reports the planned total");
        }

        var leftover = await GroupsStore.GetArchivedGroup(group.Id, null, null, cancellationToken);
        Assert.That(leftover.Results?.Count, Is.EqualTo(2 * BatchSize),
            "members that joined late are left for a later operation, and the loop must not run past its budget");
    }

    static FailedMessage.FailureGroup NewGroup() =>
        new() { Id = Guid.NewGuid().ToString(), Title = "OrderPlaced", Type = "Message Type" };

    static IngestedFailure InGroup(FailedMessage.FailureGroup group) =>
        new()
        {
            Groups = [group],
            AttemptedAt = Noon,
            TimeOfFailure = Noon,
            TimeSent = Noon.AddMinutes(-1)
        };

    async Task Insert(FailedMessage.FailureGroup group, int count, FailedMessageStatus status)
    {
        var failures = Enumerable.Range(0, count).Select(_ => InGroup(group)).Select(f => f.ToFailedMessage(status)).ToArray();

        foreach (var message in failures)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(failures);
        await CompleteDatabaseOperation();
    }

    /// <summary>
    /// Records every domain event and offers a hook that runs when each is raised. Cancellation is
    /// honoured on the way in so a runaway loop is cut short once the test timeout fires.
    /// </summary>
    sealed class ArrivalsDomainEvents : IDomainEvents
    {
        public Func<object, Task> OnRaised { get; set; } = _ => Task.CompletedTask;

        public System.Collections.Generic.List<object> Raised { get; } = [];

        public int ArrivalInjections { get; set; }

        public async Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            cancellationToken.ThrowIfCancellationRequested();

            Raised.Add(domainEvent);
            await OnRaised(domainEvent);
        }
    }
}