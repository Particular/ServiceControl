namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Base class for data stores that provides helper methods to simplify scope and DbContext management
/// </summary>
public abstract class DataStoreBase
{
    protected readonly IServiceScopeFactory scopeFactory;

    protected DataStoreBase(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, returning a result
    /// </summary>
    protected async Task<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContextBase, Task<T>> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();// Use CreateAsyncScope for async disposal
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
        return await operation(dbContext);
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async Task ExecuteWithDbContext(Func<ServiceControlDbContextBase, Task> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();
        await operation(dbContext);
    }

    /// <summary>
    /// Creates a scope for operations that need to manage their own scope lifecycle (e.g., managers)
    /// </summary>
    protected IServiceScope CreateScope() => scopeFactory.CreateAsyncScope();
}
