namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

static class SqlServerSharedContainer
{
    const string docsPath = "docs/testing-persistence.md#sql-server";

    /// <summary>
    /// Tests share one database and take a schema each, so the connection string is used as it
    /// comes and the database it names has to exist. The container has only master until this
    /// creates one.
    /// </summary>
    const string testDatabaseName = "ServiceControlTests";

    public static async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        var envConnStr = Environment.GetEnvironmentVariable("ServiceControl_Persistence_SqlServer_ConnectionString");
        if (!string.IsNullOrEmpty(envConnStr))
        {
            return envConnStr;
        }

        if (connectionString != null)
        {
            return connectionString;
        }

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (connectionString == null)
            {
                container ??= await StartContainerAsync(cancellationToken);
                connectionString = await CreateTestDatabase(container.GetConnectionString(), cancellationToken);
            }

            return connectionString;
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

    static async Task<string> CreateTestDatabase(string serverConnectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(serverConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{testDatabaseName}') IS NULL CREATE DATABASE [{testDatabaseName}]";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = testDatabaseName }.ConnectionString;
    }

    static MsSqlContainer container;
    static string connectionString;
    static readonly SemaphoreSlim semaphore = new(1, 1);
}
