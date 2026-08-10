namespace ServiceControl.Persistence.EFCore.Implementation;

using DbContexts;
using Microsoft.Extensions.DependencyInjection;

public class EditFailedMessagesDataStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) : IEditFailedMessagesDataStore
{
    public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager()
    {
        var scope = scopeFactory.CreateAsyncScope();
        return Task.FromResult<IEditFailedMessagesManager>(
            new EditFailedMessagesManager(scope, scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>(), timeProvider));
    }
}