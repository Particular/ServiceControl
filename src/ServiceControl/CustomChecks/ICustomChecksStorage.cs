namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;
    using Infrastructure;

    interface ICustomChecksStorage
    {
        Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail);

        Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string status = null);
    }
}