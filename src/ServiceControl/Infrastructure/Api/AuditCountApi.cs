namespace ServiceControl.Infrastructure.Api
{
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.CompositeViews.MessageCounting;
    using ServiceControl.Persistence.Infrastructure;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class AuditCountApi(GetAuditCountsForEndpointApi auditCountsForEndpointApi) : IAuditCountApi
    {
        public async Task<IList<AuditCount>> GetEndpointAuditCounts(int? page, int? pageSize, string endpoint)
        {
            var pagingInfo = new PagingInfo(page, pageSize);
            QueryResult<IList<AuditCount>> result = await auditCountsForEndpointApi.Execute(new AuditCountsForEndpointContext(pagingInfo, endpoint), $"/api/endpoints/{endpoint}/audit-count");

            return result.Results;
        }
    }
}
