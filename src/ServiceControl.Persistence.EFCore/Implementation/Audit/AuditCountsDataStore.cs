namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Api.Contracts;
using ServiceControl.Persistence.Infrastructure;

// Mirrors the RavenDB audit persister: the last thirty days of successful, non-system messages
// received by the endpoint, a count per UTC day, and a single zero for today when the endpoint only
// ever sent, so the licensing collector still learns the endpoint exists.
public class AuditCountsDataStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : DataStoreBase(scopeFactory), IAuditCountsDataStore
{
    static readonly TimeSpan Window = TimeSpan.FromDays(30);

    public Task<QueryResult<IList<AuditCount>>> QueryAuditCounts(string endpointName, CancellationToken cancellationToken = default) =>
        ExecuteQueryWithDbContext(async (dbContext, token) =>
        {
            var today = timeProvider.GetUtcNow().UtcDateTime.Date;
            var from = today - Window;

            // The partition key bounds the scan to the window's partitions; the grouping is on the
            // processing day, which is what the count is meant to be of.
            var counts = await dbContext.AuditMessages
                .AsNoTracking()
                .Where(message => message.ReceivingEndpointName == endpointName
                    && !message.IsSystemMessage
                    && message.CreatedOn >= from
                    && message.ProcessedAt >= from)
                .GroupBy(message => message.ProcessedAt.Date)
                .Select(group => new AuditCount { UtcDate = group.Key, Count = group.LongCount() })
                .OrderBy(count => count.UtcDate)
                .ToListAsync(token);

            if (counts.Count == 0 && await dbContext.AuditMessages.AsNoTracking().AnyAsync(message => message.SendingEndpointName == endpointName, token))
            {
                counts.Add(new AuditCount { UtcDate = today, Count = 0 });
            }

            IList<AuditCount> results = counts;

            return new QueryResult<IList<AuditCount>>(results, new QueryStatsInfo(DataVersion.OverRows([("days", results.Count)], results, count => [count.UtcDate, count.Count]), results.Count));
        }, cancellationToken);
}
