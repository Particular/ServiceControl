namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Infrastructure;

    public interface IQueueAddressStore
    {
        Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo, CancellationToken cancellationToken = default);
    }
}