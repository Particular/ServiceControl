namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;

static class SqlServerSharedContainer
{
    const string docsPath = "docs/testing-persistence.md#sql-server";

    public static async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
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

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            container ??= await StartContainerAsync(cancellationToken);
            return container.GetConnectionString();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task Stop(CancellationToken cancellationToken = default) => await (container?.DisposeAsync() ?? ValueTask.CompletedTask);

    static async Task<MsSqlContainer> StartContainerAsync(CancellationToken cancellationToken)
    {
        var c = new MsSqlBuilder("particular/servicecontrol-testing-sqlserver:latest").Build();
        try
        {
            await c.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start SQL Server persistence test container. See {docsPath} for setup instructions.",
                ex);
        }

        return c;
    }

    static MsSqlContainer container;
    static readonly SemaphoreSlim semaphore = new(1, 1);
}
