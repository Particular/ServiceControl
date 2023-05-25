namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure;
    using ServiceControl.Contracts.CustomChecks;

    public interface ICustomChecksDataStore
    {
        Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail);

        Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null);
        Task DeleteCustomCheck(Guid id);
    }
}