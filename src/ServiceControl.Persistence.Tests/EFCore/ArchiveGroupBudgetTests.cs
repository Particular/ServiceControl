namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Recoverability;

/// <summary>
/// EF Core archives a failure group with a batch loop over live group membership, so batch counts
/// can overrun the totals planned when the operation started. These tests pin the invariants of
/// the bounded loop: the persisted total is widened when the processed count overruns it (so the
/// reported remaining count never goes negative, and a resume starts from the widened total), only
/// rows actually affected by the re-asserted status update are counted as processed, and a resumed
/// operation whose plan is already complete does not process a new batch.
/// </summary>
[TestFixture]
class ArchiveGroupBudgetTests : ErrorIngestionTestBase
{
    static readonly DateTime Noon = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    readonly HookingDomainEvents events = new();
    readonly FlipOneUnresolvedRowBeforeStatusUpdate flipOneRow = new();

    public ArchiveGroupBudgetTests()
    {
        var registerServices = RegisterServices;
        RegisterServices = services =>
        {
            registerServices(services);
            services.AddSingleton<IDomainEvents>(events);
            PersistenceTestsContext.InterceptDatabaseCommands(services, flipOneRow);
        };
    }

    [TearDown]
    public void DisarmInterceptor() => flipOneRow.Disarm();

    [Test]
    [CancelAfter(180_000)]
    public async Task A_batch_overrunning_the_planned_total_widens_the_persisted_total()
    {
        var group = NewGroup();
        await Insert(group, 1500, FailedMessageStatus.Unresolved);

        var snapshots = new List<ArchiveOperationEntity>();
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchArchived)
            {
                if (snapshots.Count == 0)
                {
                    // 700 failures join the group after the plan (1500) was captured
                    await Insert(group, 700, FailedMessageStatus.Unresolved);
                }

                snapshots.Add(await Query(db => db.ArchiveOperations.AsNoTracking().SingleAsync()));
            }
        };

        await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: TestContext.CurrentContext.CancellationToken);

        await CompleteDatabaseOperation();

        var progress = events.Raised.OfType<ArchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupArchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshots, Has.Count.EqualTo(2), "the planned batch budget (ceil(1500/1000)) caps the loop at two batches");
            Assert.That(progress, Has.All.Matches<ArchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress.Last().TotalNumberOfMessages, Is.EqualTo(2000), "the reported total adopts the processed count");
            Assert.That(progress.Last().NumberOfMessagesArchived, Is.EqualTo(2000));
            Assert.That(snapshots.Last().TotalNumberOfMessages, Is.EqualTo(2000), "the persisted total must adopt the processed count, so a resume starts from it");
            Assert.That(snapshots.Last().NumberOfMessagesProcessed, Is.EqualTo(2000));
            Assert.That(completed.MessagesCount, Is.EqualTo(2000), "the completion event reports the widened total");
        }

        using (Assert.EnterMultipleScope())
        {
            var leftover = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);
            Assert.That(leftover.Results?.Count, Is.EqualTo(200), "members that joined late are left for a later operation");
            Assert.That(await Query(db => db.ArchiveOperations.CountAsync()), Is.Zero, "the operation row is removed on completion");
        }
    }

    [Test]
    [CancelAfter(180_000)]
    public async Task An_unarchive_batch_overrunning_the_planned_total_widens_the_persisted_total()
    {
        var group = NewGroup();
        await Insert(group, 1500, FailedMessageStatus.Archived);

        var snapshots = new List<ArchiveOperationEntity>();
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchUnarchived)
            {
                if (snapshots.Count == 0)
                {
                    // 700 failures join the group after the plan (1500) was captured
                    await Insert(group, 700, FailedMessageStatus.Archived);
                }

                snapshots.Add(await Query(db => db.ArchiveOperations.AsNoTracking().SingleAsync()));
            }
        };

        await ArchiveMessages.UnarchiveAllInGroup(group.Id, cancellationToken: TestContext.CurrentContext.CancellationToken);

        await CompleteDatabaseOperation();

        var progress = events.Raised.OfType<UnarchiveOperationBatchCompleted>().Select(e => e.Progress).ToArray();
        var completed = events.Raised.OfType<FailedMessageGroupUnarchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshots, Has.Count.EqualTo(2), "the planned batch budget (ceil(1500/1000)) caps the loop at two batches");
            Assert.That(progress, Has.All.Matches<UnarchiveProgress>(p => p.MessagesRemaining >= 0), "remaining must never go negative");
            Assert.That(progress.Last().TotalNumberOfMessages, Is.EqualTo(2000), "the reported total adopts the processed count");
            Assert.That(progress.Last().NumberOfMessagesUnarchived, Is.EqualTo(2000));
            Assert.That(snapshots.Last().TotalNumberOfMessages, Is.EqualTo(2000), "the persisted total must adopt the processed count, so a resume starts from it");
            Assert.That(snapshots.Last().NumberOfMessagesProcessed, Is.EqualTo(2000));
            Assert.That(completed.MessagesCount, Is.EqualTo(2000), "the completion event reports the widened total");
        }

        using (Assert.EnterMultipleScope())
        {
            var leftover = await GroupsStore.GetArchivedGroup(group.Id, null, null);
            Assert.That(leftover.Results?.Count, Is.EqualTo(200), "members that joined late are left for a later operation");
            Assert.That(await Query(db => db.ArchiveOperations.CountAsync()), Is.Zero, "the operation row is removed on completion");
        }
    }

    [Test]
    [CancelAfter(180_000)]
    public async Task Only_rows_still_in_the_source_status_are_counted_as_processed()
    {
        var group = NewGroup();
        await Insert(group, 3, FailedMessageStatus.Unresolved);

        var snapshots = new List<ArchiveOperationEntity>();
        events.OnRaised = async domainEvent =>
        {
            if (domainEvent is FailedMessageGroupBatchArchived)
            {
                snapshots.Add(await Query(db => db.ArchiveOperations.AsNoTracking().SingleAsync()));
            }
        };

        // Between the fetch of the batch and the bulk status update, a concurrent change moves one
        // group member out of the source status. The re-asserted update must skip that row and the
        // progress counters must only claim the rows the update actually affected.
        flipOneRow.Arm(async () =>
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var victim = await dbContext.FailedMessages
                .Where(fm => fm.Status == FailedMessageStatus.Unresolved
                             && dbContext.FailedMessageGroups.Any(g => g.GroupId == group.Id && g.FailedMessageUniqueId == fm.UniqueMessageId))
                .OrderBy(fm => fm.UniqueMessageId)
                .FirstAsync();
            await dbContext.FailedMessages
                .Where(fm => fm.UniqueMessageId == victim.UniqueMessageId)
                .ExecuteUpdateAsync(s => s.SetProperty(fm => fm.Status, FailedMessageStatus.Resolved));
        });

        await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: TestContext.CurrentContext.CancellationToken);

        Assert.That(flipOneRow.WasTriggered, Is.True, "the status update must pass through the race interceptor");
        var batchEvent = events.Raised.OfType<FailedMessageGroupBatchArchived>().Single();
        var progress = events.Raised.OfType<ArchiveOperationBatchCompleted>().Select(e => e.Progress).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batchEvent.FailedMessagesIds, Has.Length.EqualTo(3), "the fetch selected all three group members");
            Assert.That(progress.NumberOfMessagesArchived, Is.EqualTo(2), "only the rows the status update affected are counted as archived");
            Assert.That(progress.MessagesRemaining, Is.EqualTo(1), "the concurrently changed row is not claimed as processed");
            Assert.That(progress.Percentage, Is.EqualTo(Math.Round(2 / 3.0, 2)));
            Assert.That(progress.TotalNumberOfMessages, Is.EqualTo(3), "the plan is not widened when nothing overran it");
            Assert.That(snapshots.Single().NumberOfMessagesProcessed, Is.EqualTo(2), "the persisted checkpoint counts affected rows only");
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Query(db => db.FailedMessages.CountAsync(fm => fm.Status == FailedMessageStatus.Archived)), Is.EqualTo(2));
            Assert.That(await Query(db => db.FailedMessages.CountAsync(fm => fm.Status == FailedMessageStatus.Resolved)), Is.EqualTo(1), "the concurrently changed row keeps its new status");
        }
    }

    [Test]
    [CancelAfter(180_000)]
    public async Task A_resumed_operation_whose_plan_is_already_complete_does_not_process_a_new_batch()
    {
        var group = NewGroup();

        // A crash between the last batch and finalization leaves the plan complete but unfinalized;
        // the row was persisted by a version that did not reconcile the plan with what it processed
        await Store(new ArchiveOperationEntity
        {
            RequestId = group.Id,
            GroupName = group.Title,
            ArchiveType = ArchiveType.FailureGroup,
            OperationType = ArchiveOperationType.Archive,
            TotalNumberOfMessages = 2,
            NumberOfMessagesProcessed = 3,
            NumberOfBatches = 1,
            CurrentBatch = 1,
            Started = Now
        });

        await Insert(group, 2, FailedMessageStatus.Unresolved); // arrived after the plan was completed

        await ArchiveMessages.ArchiveAllInGroup(group.Id, cancellationToken: TestContext.CurrentContext.CancellationToken);

        var starting = events.Raised.OfType<ArchiveOperationStarting>().Single();
        var completed = events.Raised.OfType<FailedMessageGroupArchived>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(events.Raised.OfType<FailedMessageGroupBatchArchived>(), Is.Empty, "no new batch may be processed once the planned total has been reached");
            Assert.That(starting.Progress.MessagesRemaining, Is.EqualTo(0), "a resumed operation must not report a negative remaining count");
            Assert.That(starting.Progress.TotalNumberOfMessages, Is.EqualTo(3), "the persisted processed count wins over the stale plan");
            Assert.That(completed.MessagesCount, Is.EqualTo(3), "the completion event reports the reconciled total");
        }

        using (Assert.EnterMultipleScope())
        {
            var leftover = await GroupsStore.GetUnresolvedGroup(group.Id, null, null);
            Assert.That(leftover.Results?.Count, Is.EqualTo(2), "the late arrivals are left for a later operation");
            Assert.That(await Query(db => db.ArchiveOperations.CountAsync()), Is.Zero, "the resumed operation row is removed on completion");
        }
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
    /// Records every domain event and offers a hook that runs synchronously when each is raised.
    /// </summary>
    sealed class HookingDomainEvents : IDomainEvents
    {
        public Func<object, Task> OnRaised { get; set; } = _ => Task.CompletedTask;

        public List<object> Raised { get; } = [];

        public async Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            cancellationToken.ThrowIfCancellationRequested();

            Raised.Add(domainEvent);
            await OnRaised(domainEvent);
        }
    }

    /// <summary>
    /// Arms a one-shot change that runs between the archiver's batch fetch and its bulk status
    /// update, simulating a concurrent status change on a row the fetch already selected. EF
    /// renders that update as the only UPDATE on FailedMessages that sets StatusChangedAt.
    /// </summary>
    sealed class FlipOneUnresolvedRowBeforeStatusUpdate : DbCommandInterceptor
    {
        Func<Task> armed;
        public bool WasTriggered { get; private set; }

        public void Arm(Func<Task> flip) => Interlocked.Exchange(ref armed, flip);

        public void Disarm() => Interlocked.Exchange(ref armed, null);

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await FlipBeforeUpdate(command);
            return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await FlipBeforeUpdate(command);
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            await FlipBeforeUpdate(command);
            return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }

        async Task FlipBeforeUpdate(DbCommand command)
        {
            if (IsFailedMessagesStatusUpdate(command) && Interlocked.Exchange(ref armed, null) is { } flip)
            {
                await flip();
                WasTriggered = true;
            }
        }

        // Seed inserts and the operation-row bookkeeping (INSERT/UPDATE/DELETE on ArchiveOperations)
        // do not match; the command shape is checked before the one-shot arm is consumed.
        static bool IsFailedMessagesStatusUpdate(DbCommand command)
        {
            var sql = command.CommandText.Replace("_", ""); // PostgreSQL uses snake_case names.
            return sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("FailedMessages", StringComparison.OrdinalIgnoreCase)
                   && sql.Contains("StatusChangedAt", StringComparison.OrdinalIgnoreCase);
        }
    }
}