namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;
    using ServiceControl.CustomChecks;

    interface ICustomChecksDataStore
    {
        Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail);

        Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null);
    }
}