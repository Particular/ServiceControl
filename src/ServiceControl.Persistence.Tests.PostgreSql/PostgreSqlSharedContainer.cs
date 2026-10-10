namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;

static class PostgreSqlSharedContainer
{
    const string docsPath = "docs/testing-persistence.md#postgresql";

    /// <summary>
    /// Tests share one database and take a schema each, so the connection string is used as it
    /// comes and the database it names has to exist. The container hands back its maintenance
    /// database until this creates one to keep test schemas out of it.
    /// </summary>
    const string testDatabaseName = "servicecontroltests";

    public static async Task<string> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        var envConnStr = Environment.GetEnvironmentVariable("ServiceControl_Persistence_PostgreSql_ConnectionString");
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

    static async Task<string> CreateTestDatabase(string maintenanceConnectionString, CancellationToken cancellationToken)
    {
        await using (var connection = new NpgsqlConnection(maintenanceConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            // CREATE DATABASE cannot run inside a transaction and has no IF NOT EXISTS, so the
            // existence check is a separate statement.
            command.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{testDatabaseName}'";
            if (await command.ExecuteScalarAsync(cancellationToken) is null)
            {
                command.CommandText = $"CREATE DATABASE {testDatabaseName}";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return new NpgsqlConnectionStringBuilder(maintenanceConnectionString) { Database = testDatabaseName }.ConnectionString;
    }

    public static async Task Stop(CancellationToken cancellationToken = default) => await (container?.DisposeAsync() ?? ValueTask.CompletedTask);

    static async Task<PostgreSqlContainer> StartContainerAsync(CancellationToken cancellationToken)
    {
        var c = new PostgreSqlBuilder("postgres:16-alpine")
            .Build();
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
                $"Failed to start PostgreSQL persistence test container. See {docsPath} for setup instructions.",
                ex);
        }

        return c;
    }

    static PostgreSqlContainer container;
    static string connectionString;
    static readonly SemaphoreSlim semaphore = new(1, 1);
}
