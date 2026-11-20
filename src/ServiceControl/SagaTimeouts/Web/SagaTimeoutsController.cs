namespace ServiceControl.SagaTimeouts.Web
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using NServiceBus;
    using Persistence.Infrastructure;
    using Persistence.SagaTimeouts;

    [ApiController]
    [Route("api")]
    public class SagaTimeoutsController(
        ISagaTimeoutDataStore store,
        IMessageSession session,
        TimeProvider timeProvider,
        ICurrentUserAccessor userAccessor,
        IMessageActionAuditLog auditLog) : ControllerBase
    {
        [Authorize(Policy = Permissions.ErrorTimeoutsView)]
        [Route("timeouts")]
        [HttpGet]
        public async Task<ActionResult<IList<SagaTimeoutView>>> Timeouts(
            [FromQuery] PagingInfo pagingInfo,
            [FromQuery] string status = null,
            [FromQuery] string endpoint = null,
            [FromQuery(Name = "saga_type")] string sagaType = null,
            [FromQuery(Name = "saga_id")] Guid? sagaId = null,
            [FromQuery] string q = null,
            [FromQuery] string sort = null,
            [FromQuery] string direction = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryParseStatus(status, out var listStatus))
            {
                return BadRequest(new SagaTimeoutActionError($"Unknown status '{status}'. Use pending, overdue, or all."));
            }

            var query = new SagaTimeoutQuery
            {
                Status = listStatus,
                EndpointName = endpoint,
                SagaType = sagaType,
                SagaId = sagaId,
                SearchText = q
            };

            var result = await store.Query(query, pagingInfo, ToSortInfo(sort, direction), cancellationToken);

            Response.WithQueryStatsAndPagingInfo(result.QueryStats, pagingInfo);

            return Ok(result.Results);
        }

        [Authorize(Policy = Permissions.ErrorTimeoutsView)]
        [Route("timeouts/counts")]
        [HttpGet]
        public Task<SagaTimeoutCounts> Counts(CancellationToken cancellationToken = default) => store.GetCounts(cancellationToken);

        [Authorize(Policy = Permissions.ErrorTimeoutsView)]
        [Route("timeouts/{timeoutId}")]
        [HttpGet]
        public async Task<ActionResult<SagaTimeoutDetails>> Timeout(string timeoutId, CancellationToken cancellationToken = default)
        {
            var details = await store.Get(timeoutId, cancellationToken);

            return details == null ? NotFound() : Ok(details);
        }

        [Authorize(Policy = Permissions.ErrorTimeoutsView)]
        [Route("sagas/{sagaId}/timeouts")]
        [HttpGet]
        public Task<IList<SagaTimeoutView>> SagaTimeouts(Guid sagaId, CancellationToken cancellationToken = default) => store.GetBySaga(sagaId, cancellationToken);

        [Authorize(Policy = Permissions.ErrorTimeoutsManage)]
        [Route("timeouts/{timeoutId}/reschedule")]
        [HttpPost]
        public Task<IActionResult> Reschedule(string timeoutId, [FromBody] RescheduleSagaTimeoutRequest request, CancellationToken cancellationToken = default)
        {
            if (request.DueTime == null)
            {
                return Task.FromResult<IActionResult>(BadRequest(new SagaTimeoutActionError("A reschedule needs a due_time.")));
            }

            return RequestChange(timeoutId, SagaTimeoutOperation.Reschedule, request.ExpectedVersion, request.DueTime, request.Payload, request.PayloadContentType, cancellationToken);
        }

        [Authorize(Policy = Permissions.ErrorTimeoutsManage)]
        [Route("timeouts/{timeoutId}/trigger")]
        [HttpPost]
        public Task<IActionResult> Trigger(string timeoutId, [FromBody] SagaTimeoutVersionRequest request, CancellationToken cancellationToken = default) =>
            RequestChange(timeoutId, SagaTimeoutOperation.Trigger, request.ExpectedVersion, timeProvider.GetUtcNow(), null, null, cancellationToken);

        [Authorize(Policy = Permissions.ErrorTimeoutsManage)]
        [Route("timeouts/{timeoutId}/cancel")]
        [HttpPost]
        public Task<IActionResult> Cancel(string timeoutId, [FromBody] SagaTimeoutVersionRequest request, CancellationToken cancellationToken = default) =>
            RequestChange(timeoutId, SagaTimeoutOperation.Cancel, request.ExpectedVersion, null, null, null, cancellationToken);

        [Authorize(Policy = Permissions.ErrorTimeoutsManage)]
        [Route("timeouts/{timeoutId}")]
        [HttpDelete]
        public async Task<IActionResult> Forget(string timeoutId, CancellationToken cancellationToken = default)
        {
            var user = userAccessor.Resolve(User);

            await auditLog.AuditedOperation(user, MessageActionKind.Delete, Permissions.ErrorTimeoutsManage, MessageActionScope.Single,
                resource: timeoutId, count: 1, operationId: this.AuditOperationId(),
                ct => store.Forget(timeoutId, ct), cancellationToken);

            return NoContent();
        }

        async Task<IActionResult> RequestChange(string timeoutId, SagaTimeoutOperation change, int? expectedVersion, DateTimeOffset? dueTime, string payload, string payloadContentType, CancellationToken cancellationToken)
        {
            if (expectedVersion == null)
            {
                return BadRequest(new SagaTimeoutActionError("The request needs an expected_version."));
            }

            var operationId = Guid.NewGuid();
            var result = await store.RecordOperatorRequest(
                new SagaTimeoutOperatorRequest(timeoutId, operationId, change, expectedVersion.Value, dueTime, timeProvider.GetUtcNow()),
                cancellationToken);

            switch (result.Outcome)
            {
                case SagaTimeoutOperatorRequestOutcome.NotFound:
                    return NotFound();
                case SagaTimeoutOperatorRequestOutcome.VersionMismatch:
                    return Conflict(new SagaTimeoutActionError($"The timeout is at version {result.Timeout.Version}, not {expectedVersion}."));
                case SagaTimeoutOperatorRequestOutcome.OperationPending:
                    return Conflict(new SagaTimeoutActionError("Another change of this timeout is waiting for the endpoint."));
                case SagaTimeoutOperatorRequestOutcome.NotPending:
                    return UnprocessableEntity(new SagaTimeoutActionError($"The timeout is {result.Timeout.Status}, so it cannot be changed."));
                case SagaTimeoutOperatorRequestOutcome.NoManagementAddress:
                    return UnprocessableEntity(new SagaTimeoutActionError("The endpoint has not reported the address that accepts timeout changes."));
                case SagaTimeoutOperatorRequestOutcome.Accepted:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operator request outcome: {result.Outcome}");
            }

            var timeout = result.Timeout;
            var user = userAccessor.Resolve(User);
            var auditOperationId = this.AuditOperationId();

            await auditLog.AuditedOperation(user, ToAuditKind(change), Permissions.ErrorTimeoutsManage, MessageActionScope.Single,
                resource: timeoutId, count: 1, operationId: auditOperationId,
                ct => session.Send(new RequestSagaTimeoutChange
                {
                    OperationId = operationId,
                    Change = change,
                    ManagementAddress = timeout.ManagementAddress,
                    SagaType = timeout.SagaType,
                    SagaId = timeout.SagaId,
                    TimeoutId = timeout.TimeoutId,
                    ExpectedVersion = expectedVersion.Value,
                    DueTime = dueTime,
                    Payload = payload,
                    PayloadContentType = payloadContentType
                }, AuditHeaders.LocalSendOptions(user, auditOperationId), ct), cancellationToken);

            return Accepted(new SagaTimeoutOperationAccepted(operationId));
        }

        static MessageActionKind ToAuditKind(SagaTimeoutOperation change) => change switch
        {
            SagaTimeoutOperation.Cancel => MessageActionKind.Cancel,
            SagaTimeoutOperation.Trigger => MessageActionKind.Trigger,
            SagaTimeoutOperation.Reschedule => MessageActionKind.Reschedule,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null)
        };

        static bool TryParseStatus(string status, out SagaTimeoutListStatus listStatus)
        {
            listStatus = SagaTimeoutListStatus.Pending;
            switch (status)
            {
                case null or "" or "pending":
                    return true;
                case "overdue":
                    listStatus = SagaTimeoutListStatus.Overdue;
                    return true;
                case "all":
                    listStatus = SagaTimeoutListStatus.All;
                    return true;
                default:
                    return false;
            }
        }

        // Timeouts have their own sort options and sort ascending by default, so the shared SortInfo binder does not fit
        static SortInfo ToSortInfo(string sort, string direction) => new(
            Array.IndexOf(SagaTimeoutQuery.SortOptions, sort) >= 0 ? sort : "due_time",
            direction == "desc" ? "desc" : "asc");
    }

    public class RescheduleSagaTimeoutRequest
    {
        public DateTimeOffset? DueTime { get; set; }
        public int? ExpectedVersion { get; set; }
        public string Payload { get; set; }
        public string PayloadContentType { get; set; }
    }

    public class SagaTimeoutVersionRequest
    {
        public int? ExpectedVersion { get; set; }
    }

    public record SagaTimeoutOperationAccepted(Guid OperationId);

    public record SagaTimeoutActionError(string Detail);
}
