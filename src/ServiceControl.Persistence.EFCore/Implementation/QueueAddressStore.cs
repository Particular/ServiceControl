namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

public class QueueAddressStore : IQueueAddressStore
{
    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo)
    {
        await exe
        var queryable 
    }

    public Task<QueryResult<IList<QueueAddress>>> GetAddressesBySearchTerm(string search, PagingInfo pagingInfo) =>
        throw new NotImplementedException();
}