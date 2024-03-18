namespace ServiceControl.Infrastructure.Api;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CompositeViews.MessageCounting;
using Persistence.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

class AuditCountApi(GetAuditCountsForEndpointApi auditCountsForEndpointApi) : IAuditCountApi
{
    public async Task<IList<AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken token) =>
        (await auditCountsForEndpointApi.Execute(new AuditCountsForEndpointContext(new PagingInfo(), endpoint),
            $"/api/endpoints/{endpoint}/audit-count")).Results;
}