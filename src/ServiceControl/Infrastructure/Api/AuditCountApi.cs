namespace ServiceControl.Infrastructure.Api
{
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.CompositeViews.MessageCounting;
    using ServiceControl.Persistence.Infrastructure;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    class AuditCountApi(GetAuditCountsForEndpointApi auditCountsForEndpointApi) : IAuditCountApi
    {
        public async Task<IList<AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken token) => (await auditCountsForEndpointApi.Execute(new(new PagingInfo(), endpoint), $"/api/endpoints/{endpoint}/audit-count")).Results;
    }
}
