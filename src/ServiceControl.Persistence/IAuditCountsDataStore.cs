namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Persistence.Infrastructure;

    public interface IAuditCountsDataStore
    {
        Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default);
    }
}
