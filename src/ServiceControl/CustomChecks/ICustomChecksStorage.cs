namespace ServiceControl.CustomChecks
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using CompositeViews.Messages;
    using Contracts.CustomChecks;

    interface ICustomChecksStorage
    {
        Task UpdateCustomCheckStatus(CustomCheckDetail detail);

        Task<QueryResult<IList<CustomCheck>>> GetStats(HttpRequestMessage request, string status = null);
    }
}