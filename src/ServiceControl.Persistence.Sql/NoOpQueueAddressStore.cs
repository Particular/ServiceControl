namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

class NoOpQueueAddressStore : IQueueAddressStore
{
    static readonly QueryResult<IList<QueueAddress>> EmptyResult =
        new([], QueryStatsInfo.Zero);

    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo) =>
        Task.FromResult(EmptyResult);

    public Task<QueryResult<IList<QueueAddress>>> GetAddressesBySearchTerm(string search, PagingInfo pagingInfo) =>
        Task.FromResult(EmptyResult);
}
