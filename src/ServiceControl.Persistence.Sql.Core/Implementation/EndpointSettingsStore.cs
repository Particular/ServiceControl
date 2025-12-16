namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbContexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;

public class EndpointSettingsStore : DataStoreBase, IEndpointSettingsStore
{
    public EndpointSettingsStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public async IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings()
    {
        // Note: IAsyncEnumerable methods need direct scope management as they yield results
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

        var entities = dbContext.EndpointSettings.AsNoTracking().AsAsyncEnumerable();

        await foreach (var entity in entities)
        {
            yield return new EndpointSettings
            {
                Name = entity.Name,
                TrackInstances = entity.TrackInstances
            };
        }
    }

    public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Use EF's change tracking for upsert
            var existing = await dbContext.EndpointSettings.FindAsync([settings.Name], cancellationToken);
            if (existing == null)
            {
                var entity = new EndpointSettingsEntity
                {
                    Name = settings.Name,
                    TrackInstances = settings.TrackInstances
                };
                dbContext.EndpointSettings.Add(entity);
            }
            else
            {
                existing.TrackInstances = settings.TrackInstances;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }

    public Task Delete(string name, CancellationToken cancellationToken)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            await dbContext.EndpointSettings
                .Where(e => e.Name == name)
                .ExecuteDeleteAsync(cancellationToken);
        });
    }
}
