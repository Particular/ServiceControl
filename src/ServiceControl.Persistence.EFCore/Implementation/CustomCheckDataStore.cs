namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Text;
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
        var customCheck = await context.CustomChecks.FindAsync(detail.GetDeterministicId());

        if (customCheck == null ||
            (customCheck.Status == Status.Fail && !detail.HasFailed) ||
            (customCheck.Status == Status.Pass && detail.HasFailed))
        {
            if (customCheck == null)
            {
                customCheck = new CustomCheckEntity
                {
                    Id = detail.GetDeterministicId(),
                    CustomCheckId = detail.CustomCheckId,
                    Category = detail.Category,
                    OriginatingEndpointName = detail.OriginatingEndpoint.Name,
                    OriginatingEndpointHost = detail.OriginatingEndpoint.Host
                };
                context.CustomChecks.Add(customCheck);
            }

            status = CheckStateChange.Changed;
        }

        customCheck.CustomCheckId = detail.CustomCheckId;
        customCheck.Category = detail.Category;
        customCheck.Status = detail.HasFailed ? Status.Fail : Status.Pass;
        customCheck.ReportedAt = detail.ReportedAt;
        customCheck.FailureReason = detail.FailureReason;
        customCheck.OriginatingEndpointHost = detail.OriginatingEndpoint.Host;
        customCheck.OriginatingEndpointHostId = detail.OriginatingEndpoint.HostId;
        customCheck.OriginatingEndpointName = detail.OriginatingEndpoint.Name;
        await context.SaveChangesAsync();
        return status;
    });

    public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string? status = null) => ExecuteWithDbContext(async context =>
    {
        var query = context.CustomChecks.AsQueryable();

        query = status switch
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

    public Task DeleteCustomCheck(Guid id) => ExecuteWithDbContext(async context =>
    {
        var customCheck = await context.CustomChecks.FindAsync(id);
        if (customCheck != null)
        {
            context.CustomChecks.Remove(customCheck);
            await context.SaveChangesAsync();
        }
    });

    public Task<int> GetNumberOfFailedChecks() => ExecuteWithDbContext(async context => await context.CustomChecks.CountAsync(p => p.Status == Status.Fail));
}
