namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Infrastructure;

abstract class DataStoreBase(IServiceScopeFactory scopeFactory, EFPersisterSettings settings)
{
    protected async Task<T> ExecuteWithDbContext<T>(Func<AuditDbContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await operation(dbContext, cancellationToken);
    }

    protected async Task ExecuteWithDbContext(Func<AuditDbContext, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await operation(dbContext, cancellationToken);
    }

    protected async Task<T> ExecuteQueryWithDbContext<T>(Func<AuditDbContext, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        dbContext.Database.SetCommandTimeout(settings.QueryTimeout);
        return await QueryTimeLimit.Run(token => query(dbContext, token), settings.QueryTimeout, EFPersistenceConfigurationBase.QueryTimeoutSettingName, cancellationToken);
    }
}
