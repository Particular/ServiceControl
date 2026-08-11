namespace ServiceControl.Persistence.EFCore.Implementation;

using DbContexts;
using Microsoft.Extensions.DependencyInjection;

public class NotificationsDataStore(IServiceProvider serviceProvider) : INotificationsDataStore
{
    public Task<INotificationsManager> CreateNotificationsManager(CancellationToken cancellationToken = default)
    {
        var scope = serviceProvider.CreateAsyncScope();
        ServiceControlDbContext serviceControlDbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        return Task.FromResult<INotificationsManager>(new NotificationsManager(scope, serviceControlDbContext));
    }
}
