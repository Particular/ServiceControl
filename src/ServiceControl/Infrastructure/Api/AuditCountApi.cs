namespace ServiceControl.Infrastructure.Api;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompositeViews.MessageCounting;
using CompositeViews.Messages;
using Persistence.Infrastructure;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

class AuditCountApi(GetAuditCountsForEndpointApi auditCountsForEndpointApi) : IAuditCountApi
{
    public async Task<IList<AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken cancellationToken = default)
    {
        var result = await auditCountsForEndpointApi.Execute(new AuditCountsForEndpointContext(new PagingInfo(), endpoint),
            $"/api/endpoints/{endpoint}/audit-count", cancellationToken);

        // A sum that is missing an instance is not that endpoint's throughput. Recorded as such, the day would
        // be under-counted for good; failing leaves it to be collected on the next run.
        if (result.IncompleteInstances.Count > 0)
        {
            var message = $"The audit counts for endpoint '{endpoint}' are incomplete: {ScatterGatherApiBase.Describe(result.IncompleteInstances)}";

            throw result.IncompleteInstances.Any(instance => instance.Reason == QueryFailure.TimedOut)
                ? new TimeoutException(message)
                : new InvalidOperationException(message);
        }

        return result.Results;
    }
}