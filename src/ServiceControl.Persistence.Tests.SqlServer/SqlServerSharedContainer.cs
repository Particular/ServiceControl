namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.MsSql;

static class SqlServerSharedContainer
{
    public static async Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        var envConnStr = Environment.GetEnvironmentVariable("ServiceControl_Persistence_SqlServer_ConnectionString");
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

    static async Task<MsSqlContainer> StartContainerAsync(CancellationToken ct)
    {
        var c = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
            .Build();
        await c.StartAsync(ct);
        return c;
    }

    static MsSqlContainer container;
    static readonly SemaphoreSlim semaphore = new(1, 1);
}
