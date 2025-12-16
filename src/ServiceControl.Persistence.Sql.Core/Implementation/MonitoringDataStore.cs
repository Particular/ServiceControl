namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Operations;
using ServiceControl.Persistence;

public class MonitoringDataStore : DataStoreBase, IMonitoringDataStore
{
    public MonitoringDataStore(IServiceScopeFactory scopeFactory) : base(scopeFactory)
    {
    }

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
                EndpointName = endpoint.Name,
                HostId = endpoint.HostId,
                Host = endpoint.Host,
                HostDisplayName = endpoint.Host,
                Monitored = false
            };

            await dbContext.KnownEndpoints.AddAsync(knownEndpoint);
            await dbContext.SaveChangesAsync();
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
                    EndpointName = endpoint.Name,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host,
                    HostDisplayName = endpoint.Host,
                    Monitored = true
                };
                await dbContext.KnownEndpoints.AddAsync(knownEndpoint);
            }
            else
            {
                knownEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
            }

            await dbContext.SaveChangesAsync();
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
            var endpoints = await dbContext.KnownEndpoints.AsNoTracking().ToListAsync();

            foreach (var endpoint in endpoints)
            {
                var endpointDetails = new EndpointDetails
                {
                    Name = endpoint.EndpointName,
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
        return ExecuteWithDbContext(async dbContext =>
        {
            var entities = await dbContext.KnownEndpoints.AsNoTracking().ToListAsync();

            return (IReadOnlyList<KnownEndpoint>)entities.Select(e => new KnownEndpoint
            {
                EndpointDetails = new EndpointDetails
                {
                    Name = e.EndpointName,
                    HostId = e.HostId,
                    Host = e.Host
                },
                HostDisplayName = e.HostDisplayName,
                Monitored = e.Monitored
            }).ToList();
        });
    }
}
