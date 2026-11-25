namespace ServiceControl.Persistence.Tests.SagaTimeouts;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.SagaTimeouts;

[TestFixture]
class SagaTimeoutDataStoreTests : PersistenceTestBase
{
    ISagaTimeoutDataStore Store => ServiceProvider.GetRequiredService<ISagaTimeoutDataStore>();

    DateTimeOffset Clock => new(Now);

    readonly Guid sagaId = Guid.NewGuid();
    readonly string timeoutId = Guid.NewGuid().ToString();

    [Test]
    public async Task Should_create_a_pending_timeout_from_a_request_and_ignore_its_duplicate()
    {
        var requested = Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1));

        Assert.That(await Store.Apply(requested), Is.EqualTo(SagaTimeoutApplyOutcome.Applied));
        Assert.That(await Store.Apply(requested), Is.EqualTo(SagaTimeoutApplyOutcome.Duplicate));

        var details = await Store.Get(timeoutId);

        Assert.Multiple(() =>
        {
            Assert.That(details.Status, Is.EqualTo(SagaTimeoutStatus.Pending));
            Assert.That(details.Version, Is.EqualTo(1));
            Assert.That(details.LastChangeKind, Is.EqualTo(SagaTimeoutChangeKind.Requested));
            Assert.That(details.LastChangeSource, Is.EqualTo(SagaTimeoutChangeSource.Saga));
            Assert.That(details.RequestedAt, Is.Not.Null);
            Assert.That(details.ManagementAddress, Is.EqualTo("Sales.SagaTimeouts"));
            Assert.That(details.Payload, Is.EqualTo("{\"Version\":1}"));
            Assert.That(details.PayloadContentType, Is.EqualTo("application/json"));
            Assert.That(details.History, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Should_keep_the_newest_version_when_an_older_change_arrives_late()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Rescheduled, 3, dueIn: TimeSpan.FromHours(3)));

        var outcome = await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));

        var details = await Store.Get(timeoutId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(SagaTimeoutApplyOutcome.HistoryOnly));
            Assert.That(details.Version, Is.EqualTo(3));
            Assert.That(details.LastChangeKind, Is.EqualTo(SagaTimeoutChangeKind.Rescheduled));
            Assert.That(details.Payload, Is.EqualTo("{\"Version\":3}"));
            Assert.That(details.RequestedAt, Is.Not.Null, "The late request still tells when the timeout was requested");
            Assert.That(details.History.Select(entry => entry.Kind), Is.EquivalentTo(new[] { SagaTimeoutChangeKind.Requested, SagaTimeoutChangeKind.Rescheduled }));
        });
    }

    [Test]
    public async Task Should_let_a_cancel_win_over_a_reschedule_of_the_same_version()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));
        await Store.Apply(Change(SagaTimeoutChangeKind.Canceled, 2));
        await Store.Apply(Change(SagaTimeoutChangeKind.Rescheduled, 2, dueIn: TimeSpan.FromHours(2)));

        var details = await Store.Get(timeoutId);

        Assert.Multiple(() =>
        {
            Assert.That(details.Status, Is.EqualTo(SagaTimeoutStatus.Canceled));
            Assert.That(details.History, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task Should_list_pending_overdue_and_finished_timeouts_by_status()
    {
        var overdueId = Guid.NewGuid().ToString();
        var triggeredId = Guid.NewGuid().ToString();

        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromMinutes(-10)) with { TimeoutId = overdueId });
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromMinutes(-1)) with { TimeoutId = triggeredId });
        await Store.Apply(Change(SagaTimeoutChangeKind.Triggered, 1) with { TimeoutId = triggeredId });
        await CompleteDatabaseOperation();

        var pending = await QueryIds(SagaTimeoutListStatus.Pending);
        var overdue = await QueryIds(SagaTimeoutListStatus.Overdue);
        var all = await QueryIds(SagaTimeoutListStatus.All);
        var counts = await Store.GetCounts();

        Assert.Multiple(() =>
        {
            Assert.That(pending, Is.EqualTo(new[] { timeoutId }));
            Assert.That(overdue, Is.EqualTo(new[] { overdueId }));
            Assert.That(all, Is.EquivalentTo(new[] { timeoutId, overdueId, triggeredId }));
            Assert.That(counts.Pending, Is.EqualTo(1));
            Assert.That(counts.Overdue, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Should_remove_the_timeouts_of_a_completed_saga_and_ignore_later_reports()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));

        var completed = await Store.ApplySagaCompleted(new SagaCompletedChange("Sales", "Sales.OrderSaga", sagaId, Clock, Clock));
        var late = await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)) with { TimeoutId = Guid.NewGuid().ToString() });
        await CompleteDatabaseOperation();

        var details = await Store.Get(timeoutId);
        var sagaTimeouts = await Store.GetBySaga(sagaId);

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.EqualTo(SagaTimeoutApplyOutcome.Applied));
            Assert.That(late, Is.EqualTo(SagaTimeoutApplyOutcome.Ignored));
            Assert.That(details, Is.Null);
            Assert.That(sagaTimeouts, Is.Empty);
        });
    }

    [Test]
    public async Task Should_keep_a_rejection_for_an_unknown_timeout_out_of_the_lists()
    {
        var rejection = Rejection(Guid.NewGuid(), "TimeoutNotFound");

        Assert.That(await Store.Apply(rejection), Is.EqualTo(SagaTimeoutApplyOutcome.HistoryOnly));
        Assert.That(await Store.Apply(rejection), Is.EqualTo(SagaTimeoutApplyOutcome.Duplicate));
        await CompleteDatabaseOperation();

        var details = await Store.Get(timeoutId);
        var all = await QueryIds(SagaTimeoutListStatus.All);
        var sagaTimeouts = await Store.GetBySaga(sagaId);

        Assert.Multiple(() =>
        {
            Assert.That(details.Status, Is.EqualTo(SagaTimeoutStatus.Unknown));
            Assert.That(details.History.Single().Reason, Is.EqualTo("TimeoutNotFound"));
            Assert.That(all, Is.Empty);
            Assert.That(sagaTimeouts, Is.Empty);
        });
    }

    [Test]
    public async Task Should_record_an_operator_request_until_the_endpoint_reports_it()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));

        var operationId = Guid.NewGuid();
        var accepted = await Store.RecordOperatorRequest(Request(operationId, expectedVersion: 1));
        var whilePending = await Store.RecordOperatorRequest(Request(Guid.NewGuid(), expectedVersion: 1));

        var pendingOperation = (await Store.Get(timeoutId)).PendingOperation;

        await Store.Apply(Change(SagaTimeoutChangeKind.Rescheduled, 2, dueIn: TimeSpan.FromHours(2)) with { OperationId = operationId });

        var details = await Store.Get(timeoutId);
        var staleVersion = await Store.RecordOperatorRequest(Request(Guid.NewGuid(), expectedVersion: 1));

        Assert.Multiple(() =>
        {
            Assert.That(accepted.Outcome, Is.EqualTo(SagaTimeoutOperatorRequestOutcome.Accepted));
            Assert.That(accepted.Timeout.ManagementAddress, Is.EqualTo("Sales.SagaTimeouts"));
            Assert.That(whilePending.Outcome, Is.EqualTo(SagaTimeoutOperatorRequestOutcome.OperationPending));
            Assert.That(pendingOperation.OperationId, Is.EqualTo(operationId));
            Assert.That(details.PendingOperation, Is.Null);
            Assert.That(details.LastChangeSource, Is.EqualTo(SagaTimeoutChangeSource.Operator));
            Assert.That(staleVersion.Outcome, Is.EqualTo(SagaTimeoutOperatorRequestOutcome.VersionMismatch));
        });
    }

    [Test]
    public async Task Should_clear_the_pending_operation_when_the_endpoint_rejects_it()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));

        var operationId = Guid.NewGuid();
        await Store.RecordOperatorRequest(Request(operationId, expectedVersion: 1));
        await Store.Apply(Rejection(operationId, "VersionMismatch"));

        var details = await Store.Get(timeoutId);

        Assert.Multiple(() =>
        {
            Assert.That(details.PendingOperation, Is.Null);
            Assert.That(details.Status, Is.EqualTo(SagaTimeoutStatus.Pending));
            Assert.That(details.History.Last().Kind, Is.EqualTo(SagaTimeoutChangeKind.Rejected));
        });
    }

    [Test]
    public async Task Should_refuse_operator_requests_for_finished_or_missing_timeouts()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));
        await Store.Apply(Change(SagaTimeoutChangeKind.Triggered, 1));

        var finished = await Store.RecordOperatorRequest(Request(Guid.NewGuid(), expectedVersion: 1));
        var missing = await Store.RecordOperatorRequest(Request(Guid.NewGuid(), expectedVersion: 1) with { TimeoutId = "missing" });

        Assert.Multiple(() =>
        {
            Assert.That(finished.Outcome, Is.EqualTo(SagaTimeoutOperatorRequestOutcome.NotPending));
            Assert.That(missing.Outcome, Is.EqualTo(SagaTimeoutOperatorRequestOutcome.NotFound));
        });
    }

    [Test]
    public async Task Should_forget_a_timeout()
    {
        await Store.Apply(Change(SagaTimeoutChangeKind.Requested, 1, dueIn: TimeSpan.FromHours(1)));

        await Store.Forget(timeoutId);

        Assert.That(await Store.Get(timeoutId), Is.Null);
    }

    async Task<string[]> QueryIds(SagaTimeoutListStatus status)
    {
        var result = await Store.Query(new SagaTimeoutQuery { Status = status, SagaId = sagaId }, new PagingInfo(), new SortInfo("due_time", "asc"));
        return result.Results.Select(timeout => timeout.TimeoutId).ToArray();
    }

    SagaTimeoutChange Change(SagaTimeoutChangeKind kind, int version, TimeSpan? dueIn = null)
    {
        var hasPayload = kind is SagaTimeoutChangeKind.Requested or SagaTimeoutChangeKind.Rescheduled;

        return new SagaTimeoutChange
        {
            Kind = kind,
            TimeoutId = timeoutId,
            TimeoutType = "Sales.PaymentTimeout",
            Version = version,
            TriggerId = kind == SagaTimeoutChangeKind.Canceled ? null : $"trigger-{version}",
            DueTime = dueIn.HasValue ? Clock + dueIn.Value : null,
            PayloadContentType = hasPayload ? "application/json" : null,
            Payload = hasPayload ? Encoding.UTF8.GetBytes($"{{\"Version\":{version}}}") : null,
            EndpointName = "Sales",
            SagaType = "Sales.OrderSaga",
            SagaId = sagaId,
            CorrelationValue = "order-1",
            ChangedAt = Clock,
            ReceivedAt = Clock,
            Host = "host",
            HostId = "host-id",
            ManagementAddress = "Sales.SagaTimeouts"
        };
    }

    SagaTimeoutChange Rejection(Guid operationId, string reason) => new()
    {
        Kind = SagaTimeoutChangeKind.Rejected,
        TimeoutId = timeoutId,
        OperationId = operationId,
        RequestedChange = "Reschedule",
        RequestedDueTime = Clock,
        Reason = reason,
        EndpointName = "Sales",
        SagaType = "Sales.OrderSaga",
        SagaId = sagaId,
        ChangedAt = Clock,
        ReceivedAt = Clock,
        ManagementAddress = "Sales.SagaTimeouts"
    };

    SagaTimeoutOperatorRequest Request(Guid operationId, int expectedVersion) =>
        new(timeoutId, operationId, SagaTimeoutOperation.Reschedule, expectedVersion, Clock + TimeSpan.FromHours(2), Clock);
}
