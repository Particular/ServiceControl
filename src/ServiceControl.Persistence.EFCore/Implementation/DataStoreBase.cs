namespace ServiceControl.Persistence.EFCore.Implementation;

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Abstractions;
using DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Infrastructure;

/// <summary>
/// Base class for data stores that provides helper methods to simplify scope and DbContext management
/// </summary>
public abstract class DataStoreBase(IServiceScopeFactory scopeFactory)
{
    protected readonly IServiceScopeFactory scopeFactory = scopeFactory;

    /// <summary>
    /// Executes an operation with a scoped DbContext, returning a result
    /// </summary>
    protected async Task<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();// Use CreateAsyncScope for async disposal
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        return await operation(dbContext, cancellationToken);
    }

    /// <summary>
    /// Executes a read-only query with a scoped DbContext under the configured query time limit. Cancelling the
    /// query sends a cancel signal to the database server, terminating the query server-side. The limit bounds the
    /// whole data store call, which can span multiple SQL commands, to one wall-clock deadline; the provider's own
    /// per-command timeout (Database/CommandTimeout) is raised to the same value so it cannot undercut it.
    /// Only for queries: a write aborted by a deadline would be harmful.
    /// </summary>
    protected async Task<T> ExecuteQueryWithDbContext<T>(Func<ServiceControlDbContext, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<EFPersisterSettings>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        dbContext.Database.SetCommandTimeout(settings.QueryTimeout);

        return await QueryTimeLimit.Run(token => query(dbContext, token), settings.QueryTimeout, PersistenceSettings.QueryTimeoutSettingName, cancellationToken);
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async Task ExecuteWithDbContext(Func<ServiceControlDbContext, CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await operation(dbContext, cancellationToken);
    }

    /// <summary>
    /// Executes an operation with a scoped DbContext, without returning a result
    /// </summary>
    protected async IAsyncEnumerable<T> ExecuteWithDbContext<T>(Func<ServiceControlDbContext, IAsyncEnumerable<T>> operation, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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