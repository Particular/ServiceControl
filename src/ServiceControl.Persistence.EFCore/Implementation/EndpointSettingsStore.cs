namespace ServiceControl.Persistence.EFCore.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class EndpointSettingsStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEndpointSettingsStore
{
    public IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings(CancellationToken cancellationToken)
        => ExecuteWithDbContext(context => context.EndpointSettings.Select(row => new EndpointSettings
        {
            Name = row.Name,
            TrackInstances = row.TrackInstances
        }).AsAsyncEnumerable(), cancellationToken);

    public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken token) => ExecuteWithDbContext(async context =>
    {
        var entity = context.EndpointSettings.Find(settings.Name);
        if (entity == null)
        {
            entity = new EndpointSettingsEntity() { Name = settings.Name, TrackInstances = settings.TrackInstances };
            context.EndpointSettings.Add(entity);
            try
            {
                await context.SaveChangesAsync(token);
                return;
            }
            catch (DbUpdateException)
            {
                //this probably failed because of key conflict so try again
            }

            await context.Entry(entity).ReloadAsync(token);
        }

        entity.TrackInstances = settings.TrackInstances;
        await context.SaveChangesAsync(token);
    });

    public Task Delete(string name, CancellationToken cancellationToken) => ExecuteWithDbContext(async context =>
    {
        context.EndpointSettings.RemoveRange(context.EndpointSettings.Where(x => x.Name == name));
        await context.SaveChangesAsync(cancellationToken);
    });
}
