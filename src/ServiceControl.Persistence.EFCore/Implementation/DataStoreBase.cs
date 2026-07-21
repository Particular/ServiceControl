namespace ServiceControl.Persistence.EFCore.Implementation;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DbContexts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Base class for data stores that provides helper methods to simplify scope and DbContext management
/// </summary>
public abstract class DataStoreBase(IServiceScopeFactory scopeFactory)
{
    protected readonly IServiceScopeFactory scopeFactory = scopeFactory;

    /// <summary>
    /// Executes an operation with a scoped DbContext, returning a result
    /// </summary>
    protected async Task<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContext, Task<T>> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();// Use CreateAsyncScope for async disposal
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        return await operation(dbContext);
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async Task ExecuteWithDbContext(Func<ServiceControlDbContext, Task> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await operation(dbContext);
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async IAsyncEnumerable<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContext, IAsyncEnumerable<T>> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await foreach (var row in operation(dbContext))
        {
            yield return row;
        }
    }
    
    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async IAsyncEnumerable<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContext, IAsyncEnumerable<T>> operation, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await foreach (var row in operation(dbContext).WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }

    /// <summary>
    /// Creates a scope for operations that need to manage their own scope lifecycle (e.g., managers)
    /// </summary>
    protected IServiceScope CreateScope() => scopeFactory.CreateAsyncScope();
}