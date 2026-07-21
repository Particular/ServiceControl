namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;

public class MonitoringDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMonitoringDataStore
{
    public Task CreateIfNotExists(EndpointDetails endpoint)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();

            var exists = await dbContext.KnownEndpoints.AnyAsync(e => e.Id == id);
            if (exists)
            {
                return;
            }

            var knownEndpoint = new KnownEndpointEntity
            {
                Id = id,
                Name = endpoint.Name,
                HostId = endpoint.HostId,
                Host = endpoint.Host,
                Monitored = false
            };

            dbContext.KnownEndpoints.Add(knownEndpoint);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent insert with the same deterministic id may have won the race.
                // Anything else is a genuine failure.
                dbContext.Entry(knownEndpoint).State = EntityState.Detached;
                if (!await dbContext.KnownEndpoints.AnyAsync(e => e.Id == id))
                {
                    throw;
                }
            }
        });
    }

    public Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();

            var knownEndpoint = await dbContext.KnownEndpoints.FirstOrDefaultAsync(e => e.Id == id);

            if (knownEndpoint == null)
            {
                knownEndpoint = new KnownEndpointEntity
                {
                    Id = id,
                    Name = endpoint.Name,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host,
                    Monitored = true
                };
                dbContext.KnownEndpoints.Add(knownEndpoint);

                try
                {
                    await dbContext.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A concurrent insert with the same deterministic id may have won the race;
                    // fall back to the update path. Rethrow anything else.
                    dbContext.Entry(knownEndpoint).State = EntityState.Detached;
                    var monitored = endpointInstanceMonitoring.IsMonitored(id);
                    var updated = await dbContext.KnownEndpoints
                        .Where(e => e.Id == id)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Monitored, monitored));
                    if (updated == 0)
                    {
                        throw;
                    }
                }
            }
            else
            {
                knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
                await dbContext.SaveChangesAsync();
            }
        });
    }

    public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();

            await dbContext.KnownEndpoints
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Monitored, isMonitored));
        });
    }

    public Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            await foreach (var endpoint in dbContext.KnownEndpoints.AsNoTracking().AsAsyncEnumerable())
            {
                var endpointDetails = new EndpointDetails
                {
                    Name = endpoint.Name,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host
                };

                endpointInstanceMonitoring.DetectEndpointFromPersistentStore(endpointDetails, endpoint.Monitored);
            }
        });
    }

    public Task Delete(Guid endpointId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            await dbContext.KnownEndpoints
                .Where(e => e.Id == endpointId)
                .ExecuteDeleteAsync();
        });
    }

    public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints()
    {
        return ExecuteWithDbContext<IReadOnlyList<KnownEndpoint>>(async dbContext =>
            await dbContext.KnownEndpoints
                .AsNoTracking()
                .Select(e => new KnownEndpoint
                {
                    EndpointDetails = new EndpointDetails
                    {
                        Name = e.Name,
                        HostId = e.HostId,
                        Host = e.Host
                    },
                    HostDisplayName = e.Host,
                    Monitored = e.Monitored
                })
                .ToListAsync());
    }
}
