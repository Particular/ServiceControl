namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;

public class MonitoringDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMonitoringDataStore
{
    public Task CreateIfNotExists(EndpointDetails endpoint, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();

            var exists = await dbContext.KnownEndpoints.AnyAsync(e => e.Id == id, cancellationToken);
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
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent insert with the same deterministic id may have won the race.
                // Anything else is a genuine failure.
                dbContext.Entry(knownEndpoint).State = EntityState.Detached;
                if (!await dbContext.KnownEndpoints.AnyAsync(e => e.Id == id, cancellationToken))
                {
                    throw;
                }
            }
        });

    public Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();
            await dbContext.UpsertAsync([id],
                () => new KnownEndpointEntity
                {
                    Id = id,
                    Name = endpoint.Name,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host,
                    Monitored = true
                },
                knownEndpoint =>
                {
                    knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
                },
                cancellationToken
            );
        });

    public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            var id = endpoint.GetDeterministicId();

            await dbContext.KnownEndpoints
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Monitored, isMonitored), cancellationToken);
        });

    public Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async dbContext =>
        {
            await foreach (var endpoint in dbContext.KnownEndpoints.AsNoTracking().AsAsyncEnumerable().WithCancellation(cancellationToken))
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

    public Task Delete(Guid endpointId, CancellationToken cancellationToken = default)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            await dbContext.KnownEndpoints
                .Where(e => e.Id == endpointId)
                .ExecuteDeleteAsync(cancellationToken);
        });
    }

    public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints(CancellationToken cancellationToken = default)
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
                .ToListAsync(cancellationToken));
    }
}
