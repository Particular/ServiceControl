namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

class NoOpCustomChecksDataStore : ICustomChecksDataStore
{
    public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail) =>
        Task.FromResult(CheckStateChange.Unchanged);

    public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null) =>
        Task.FromResult(new QueryResult<IList<CustomCheck>>([], QueryStatsInfo.Zero));

    public Task DeleteCustomCheck(Guid id) => Task.CompletedTask;

    public Task<int> GetNumberOfFailedChecks() => Task.FromResult(0);
}
