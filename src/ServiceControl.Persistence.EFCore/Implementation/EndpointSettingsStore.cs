namespace ServiceControl.Persistence.EFCore.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class EndpointSettingsStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IEndpointSettingsStore
{
    public IAsyncEnumerable<EndpointSettings> GetAllEndpointSettings(CancellationToken cancellationToken = default)
        => ExecuteWithDbContext(context => context.EndpointSettings.Select(row => new EndpointSettings { Name = row.Name, TrackInstances = row.TrackInstances }).AsAsyncEnumerable(), cancellationToken);

    public Task UpdateEndpointSettings(EndpointSettings settings, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async context =>
            await context.UpsertAsync([settings.Name],
                () => new EndpointSettingsEntity() { Name = settings.Name, TrackInstances = settings.TrackInstances },
                entity => entity.TrackInstances = settings.TrackInstances,
                cancellationToken
            ));

    public Task Delete(string name, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(context => context.EndpointSettings.Where(x => x.Name == name)
            .ExecuteDeleteAsync(cancellationToken));
}