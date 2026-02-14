namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

public class QueueAddressStore : DataStoreBase, IQueueAddressStore
{
    public QueueAddressStore(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

    public Task<QueryResult<IList<QueueAddress>>> GetAddresses(PagingInfo pagingInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var totalCount = await dbContext.QueueAddresses.CountAsync();

            var addresses = await dbContext.QueueAddresses
                .OrderBy(q => q.PhysicalAddress)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.Next)
                .AsNoTracking()
                .Select(q => new QueueAddress
                {
                    PhysicalAddress = q.PhysicalAddress,
                    FailedMessageCount = q.FailedMessageCount
                })
                .ToListAsync();

            var queryStats = new QueryStatsInfo(string.Empty, totalCount, false);
            return new QueryResult<IList<QueueAddress>>(addresses, queryStats);
        });
    }

    public Task<QueryResult<IList<QueueAddress>>> GetAddressesBySearchTerm(string search, PagingInfo pagingInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.QueueAddresses
                .Where(q => EF.Functions.Like(q.PhysicalAddress, $"{search}%"));

            var totalCount = await query.CountAsync();

            var addresses = await query
                .OrderBy(q => q.PhysicalAddress)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.Next)
                .AsNoTracking()
                .Select(q => new QueueAddress
                {
                    PhysicalAddress = q.PhysicalAddress,
                    FailedMessageCount = q.FailedMessageCount
                })
                .ToListAsync();

            var queryStats = new QueryStatsInfo(string.Empty, totalCount, false);
            return new QueryResult<IList<QueueAddress>>(addresses, queryStats);
        });
    }
}
