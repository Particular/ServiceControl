namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;

static class PostgreSqlSharedContainer
{
    const string docsPath = "docs/testing-persistence.md#postgresql";

    public static async Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        var envConnStr = Environment.GetEnvironmentVariable("ServiceControl_Persistence_PostgreSql_ConnectionString");
        if (!string.IsNullOrEmpty(envConnStr))
        {
            return envConnStr;
        }

        if (container != null)
        {
            return container.GetConnectionString();
        }

        await semaphore.WaitAsync(ct);
        try
        {
            container ??= await StartContainerAsync(ct);
            return container.GetConnectionString();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task Stop() => await (container?.DisposeAsync() ?? ValueTask.CompletedTask);

    static async Task<PostgreSqlContainer> StartContainerAsync(CancellationToken ct)
    {
        var c = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();
        try
        {
            await c.StartAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start PostgreSQL persistence test container. See {docsPath} for setup instructions.",
                ex);
        }

        return c;
    }

    static PostgreSqlContainer container;
    static readonly SemaphoreSlim semaphore = new(1, 1);
}
