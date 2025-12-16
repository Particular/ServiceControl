namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Contracts.CustomChecks;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

public class CustomChecksDataStore : DataStoreBase, ICustomChecksDataStore
{
    public CustomChecksDataStore(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public Task<CheckStateChange> UpdateCustomCheckStatus(CustomCheckDetail detail)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var status = CheckStateChange.Unchanged;
            var id = detail.GetDeterministicId();

            var customCheck = await dbContext.CustomChecks.FirstOrDefaultAsync(c => c.Id == id);

            if (customCheck == null ||
                (customCheck.Status == (int)Status.Fail && !detail.HasFailed) ||
                (customCheck.Status == (int)Status.Pass && detail.HasFailed))
            {
                if (customCheck == null)
                {
                    customCheck = new CustomCheckEntity { Id = id };
                    await dbContext.CustomChecks.AddAsync(customCheck);
                }

                status = CheckStateChange.Changed;
            }

            customCheck.CustomCheckId = detail.CustomCheckId;
            customCheck.Category = detail.Category;
            customCheck.Status = detail.HasFailed ? (int)Status.Fail : (int)Status.Pass;
            customCheck.ReportedAt = detail.ReportedAt;
            customCheck.FailureReason = detail.FailureReason;
            customCheck.EndpointName = detail.OriginatingEndpoint.Name;
            customCheck.HostId = detail.OriginatingEndpoint.HostId;
            customCheck.Host = detail.OriginatingEndpoint.Host;

            await dbContext.SaveChangesAsync();

            return status;
        });
    }

    public Task<QueryResult<IList<CustomCheck>>> GetStats(PagingInfo paging, string? status = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.CustomChecks.AsQueryable();

            // Add status filter
            if (status == "fail")
            {
                query = query.Where(c => c.Status == (int)Status.Fail);
            }
            if (status == "pass")
            {
                query = query.Where(c => c.Status == (int)Status.Pass);
            }

            var totalCount = await query.CountAsync();

            var results = await query
                .OrderByDescending(c => c.ReportedAt)
                .Skip(paging.Offset)
                .Take(paging.Next)
                .AsNoTracking()
                .ToListAsync();

            var customChecks = results.Select(e => new CustomCheck
            {
                Id = $"{e.Id}",
                CustomCheckId = e.CustomCheckId,
                Category = e.Category,
                Status = (Status)e.Status,
                ReportedAt = e.ReportedAt,
                FailureReason = e.FailureReason,
                OriginatingEndpoint = new EndpointDetails
                {
                    Name = e.EndpointName,
                    HostId = e.HostId,
                    Host = e.Host
                }
            }).ToList();

            var queryStats = new QueryStatsInfo(string.Empty, totalCount, false);
            return new QueryResult<IList<CustomCheck>>(customChecks, queryStats);
        });
    }

    public Task DeleteCustomCheck(Guid id)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            await dbContext.CustomChecks.Where(c => c.Id == id).ExecuteDeleteAsync();
        });
    }

    public Task<int> GetNumberOfFailedChecks()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            return await dbContext.CustomChecks.CountAsync(c => c.Status == (int)Status.Fail);
        });
    }
}
