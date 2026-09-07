namespace ServiceControl.Persistence.Tests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

static class TestSchema
{
    public static Task Create(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        Execute(connectionString, CreateSchemaSql, schema, cancellationToken);

    // SQL Server has no DROP SCHEMA CASCADE, so the objects have to go first, and the full-text
    // index has to go before the table it is on.
    public static Task Drop(string connectionString, string schema, CancellationToken cancellationToken = default) =>
        Execute(connectionString, DropSchemaSql, schema, cancellationToken);

    static async Task Execute(string connectionString, string sql, string schema, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@schema", schema);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // CREATE SCHEMA has to be the only statement in its batch, and EXEC will not take a
    // concatenated expression, so the statement is built into a variable first.
    const string CreateSchemaSql = """
        IF SCHEMA_ID(@schema) IS NULL
        BEGIN
            DECLARE @create nvarchar(max) = N'CREATE SCHEMA ' + QUOTENAME(@schema);
            EXEC sp_executesql @create;
        END
        """;

    const string DropSchemaSql = """
        DECLARE @sql nvarchar(max) = N'';

        SELECT @sql = @sql + N'DROP FULLTEXT INDEX ON ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
        FROM sys.fulltext_indexes fi
        JOIN sys.tables t ON fi.object_id = t.object_id
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = @schema;

        SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
        FROM sys.foreign_keys f
        JOIN sys.tables t ON f.parent_object_id = t.object_id
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = @schema;

        SELECT @sql = @sql + N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
        FROM sys.tables t
        JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE s.name = @schema;

        IF SCHEMA_ID(@schema) IS NOT NULL
        BEGIN
            SET @sql = @sql + N'DROP SCHEMA ' + QUOTENAME(@schema) + N';';
        END

        EXEC sp_executesql @sql, N'@schema sysname', @schema = @schema;
        """;
}
