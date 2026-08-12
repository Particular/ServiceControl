namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.Contracts.CustomChecks;

    public interface ICustomChecksDataStore
    {
        Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail, CancellationToken cancellationToken = default);

        Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null, CancellationToken cancellationToken = default);
        Task DeleteCustomCheck(Guid id, CancellationToken cancellationToken = default);
        Task<int> GetNumberOfFailedChecks(CancellationToken cancellationToken = default);
    }
}