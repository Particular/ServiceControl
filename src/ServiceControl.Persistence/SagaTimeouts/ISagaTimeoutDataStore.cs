#nullable enable
namespace ServiceControl.Persistence.SagaTimeouts;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;

/// <summary>
/// Keeps the projection of the saga timeouts that endpoints report. See docs/saga-timeout-management-design.md.
/// Every method owns its session, commits before returning, and returns detached snapshots.
/// </summary>
public interface ISagaTimeoutDataStore
{
    /// <summary>
    /// Records one reported change. The current state moves forward only, ordered by (Version, Rank).
    /// Returns <see cref="SagaTimeoutApplyOutcome.Ignored"/> when the saga has completed.
    /// </summary>
    Task<SagaTimeoutApplyOutcome> Apply(SagaTimeoutChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all timeouts of the saga and writes the completed-saga marker that makes later reports for the saga ignored.
    /// </summary>
    Task<SagaTimeoutApplyOutcome> ApplySagaCompleted(SagaCompletedChange change, CancellationToken cancellationToken = default);

    Task<QueryResult<IList<SagaTimeoutView>>> Query(SagaTimeoutQuery query, PagingInfo paging, SortInfo sort, CancellationToken cancellationToken = default);

    Task<SagaTimeoutCounts> GetCounts(CancellationToken cancellationToken = default);

    Task<SagaTimeoutDetails?> Get(string timeoutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timeouts of one saga instance, except <see cref="SagaTimeoutStatus.Unknown"/> ones.
    /// </summary>
    Task<IList<SagaTimeoutView>> GetBySaga(Guid sagaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the expected version and stores the pending operation. Expected conflicts are returned as outcomes, not thrown.
    /// </summary>
    Task<SagaTimeoutOperatorRequestResult> RecordOperatorRequest(SagaTimeoutOperatorRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the timeout and its history from ServiceControl. It does not change the saga.
    /// </summary>
    Task Forget(string timeoutId, CancellationToken cancellationToken = default);
}
