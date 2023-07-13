namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Infrastructure;

    public interface IQueueAddressStore
    {
        Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo);
        Task<QueryResult<IList<QueueAddress>>> GetAddressesBySearchTerm(string search, PagingInfo pagingInfo);
    }
}