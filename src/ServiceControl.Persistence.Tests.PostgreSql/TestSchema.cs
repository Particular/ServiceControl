namespace ServiceControl.Persistence.Tests;

using System.Threading;
using System.Threading.Tasks;
using Npgsql;

static class TestSchema
{
    public static Task Create(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        Execute(connectionString, $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);

    public static Task Drop(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        Execute(connectionString, $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", cancellationToken);

    static async Task Execute(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
