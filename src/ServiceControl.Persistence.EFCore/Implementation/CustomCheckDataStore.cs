namespace ServiceControl.Persistence.EFCore.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.Infrastructure;

public class CustomCheckDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), ICustomChecksDataStore
{
    public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail) => ExecuteWithDbContext(async context =>
    {
        var status = CheckStateChange.Unchanged;

        await context.UpsertAsync([detail.GetDeterministicId()],
            () =>
            {
                status = CheckStateChange.Changed;
                return new CustomCheckEntity
                {
                    Id = detail.GetDeterministicId(),
                    CustomCheckId = detail.CustomCheckId,
                    OriginatingEndpointName = detail.OriginatingEndpoint.Name,
                    OriginatingEndpointHost = detail.OriginatingEndpoint.Host,
                    OriginatingEndpointHostId = detail.OriginatingEndpoint.HostId,
                    Status = detail.HasFailed ? Status.Fail : Status.Pass,
                    Category = detail.Category,
                    ReportedAt = detail.ReportedAt,
                    FailureReason = detail.FailureReason,
                };
            },
            entity =>
            {
                status = (entity.Status, detail.HasFailed) switch
                {
                    (Status.Fail, false) => CheckStateChange.Changed,
                    (Status.Pass, true) => CheckStateChange.Changed,
                    _ => CheckStateChange.Unchanged
                };
                //No need to update OriginatingEndpointName, OriginatingEndpointHostId, CustomCheckId
                //as they are used to generate the guid key
                entity.Status = detail.HasFailed ? Status.Fail : Status.Pass;
                entity.Category = detail.Category;
                entity.ReportedAt = detail.ReportedAt;
                entity.FailureReason = detail.FailureReason;
            });
        return status;
    });

    public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string? status = null) => ExecuteWithDbContext(async context =>
    {
        var query = context.CustomChecks.AsQueryable().AsNoTracking();

        query = status?.ToLowerInvariant() switch
        {
            "pass" => query.Where(c => c.Status == Status.Pass),
            "fail" => query.Where(c => c.Status == Status.Fail),
            _ => query
        };

        var page = await query
            .OrderBy(c => c.ReportedAt)
            .Skip(paging.Offset)
            .Take(paging.PageSize)
            .ToListAsync();

        return new QueryResult<IList<CustomCheck>>(page.Select(c => new CustomCheck
        {
            Id = c.Id.ToString(),
            CustomCheckId = c.CustomCheckId,
            Category = c.Category,
            Status = c.Status,
            ReportedAt = c.ReportedAt,
            FailureReason = c.FailureReason
        }).ToList(), new QueryStatsInfo("", page.Count, false));
    });

    public Task DeleteCustomCheck(Guid id) => ExecuteWithDbContext(async context => await context.CustomChecks.AsNoTracking().Where(cc => cc.Id == id).ExecuteDeleteAsync());

    public Task<int> GetNumberOfFailedChecks() => ExecuteWithDbContext(async context => await context.CustomChecks.AsNoTracking().CountAsync(p => p.Status == Status.Fail));
}
