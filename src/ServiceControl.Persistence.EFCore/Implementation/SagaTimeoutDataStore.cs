namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.SagaTimeouts;

// Saga timeout management is implemented for RavenDB only so far
public class SagaTimeoutDataStore : ISagaTimeoutDataStore
{
    public Task<SagaTimeoutApplyOutcome> Apply(SagaTimeoutChange change, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<SagaTimeoutApplyOutcome> ApplySagaCompleted(SagaCompletedChange change, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<QueryResult<IList<SagaTimeoutView>>> Query(SagaTimeoutQuery query, PagingInfo paging, SortInfo sort, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<SagaTimeoutCounts> GetCounts(CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<SagaTimeoutDetails?> Get(string timeoutId, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<IList<SagaTimeoutView>> GetBySaga(Guid sagaId, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task<SagaTimeoutOperatorRequestResult> RecordOperatorRequest(SagaTimeoutOperatorRequest request, CancellationToken cancellationToken = default) => throw NotSupported();

    public Task Forget(string timeoutId, CancellationToken cancellationToken = default) => throw NotSupported();

    static NotSupportedException NotSupported() => new("Saga timeout management is not supported by this persister yet.");
}
