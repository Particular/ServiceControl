namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

static class TestSchema
{
    static readonly bool SkipCleanup = string.Equals(Environment.GetEnvironmentVariable("ServiceControl_TESTS_SKIP_SCHEMA_CLEANUP"), "true", StringComparison.OrdinalIgnoreCase);

    static TestSchema()
    {
        if (SkipCleanup)
        {
            Console.WriteLine("Schema cleanup is disabled. The workflow must delete the test database after the tests finish.");
        }
    }

    public static Task Create(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        Execute(connectionString, $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);

    public static Task CreateTable(string connectionString, string schema, string table, CancellationToken cancellationToken = default) =>
        Execute(connectionString, $"CREATE TABLE \"{schema}\".\"{table}\" (id int)", cancellationToken);

    public static Task Drop(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        SkipCleanup ? Task.CompletedTask : Execute(connectionString, DropSql(schema), cancellationToken);

    public static async Task Drop(NpgsqlDataSource dataSource, string schema, CancellationToken cancellationToken = default)
    {
        if (SkipCleanup)
        {
            return;
        }

        await using var command = dataSource.CreateCommand(DropSql(schema));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    static string DropSql(string schema) => $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";

    static async Task Execute(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
