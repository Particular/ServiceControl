namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using EFCore.PostgreSql;
using Npgsql;
using NUnit.Framework;

class FullTextSearchTests : PersistenceTestBase
{
    [Test]
    public async Task Can_create_and_query_full_text_index()
    {
        var postgreSqlSettings = PersistenceSettings as PostgreSqlPersisterSettings;
        Assert.That(postgreSqlSettings, Is.Not.Null);

        var tableName = $"fts_{Guid.NewGuid():N}";
        var commandTimeoutSeconds = 30;

        await using var connection = new NpgsqlConnection(postgreSqlSettings.ConnectionString);
        await connection.OpenAsync();

        try
        {
            await ExecuteNonQuery(connection, $"""
                CREATE TABLE public."{tableName}" (
                    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    body TEXT NOT NULL,
                    search_vector tsvector GENERATED ALWAYS AS (to_tsvector('english', body)) STORED
                )
                """, commandTimeoutSeconds);

            await ExecuteNonQuery(connection, $"""CREATE INDEX "{tableName}_search_vector_idx" ON public."{tableName}" USING GIN (search_vector)""", commandTimeoutSeconds);

            await ExecuteNonQuery(connection, $"""
                INSERT INTO public."{tableName}"(body) VALUES
                ('quick brown fox jumps'),
                ('azure service bus transport')
                """, commandTimeoutSeconds);

            var matchCount = await ExecuteScalarInt(connection, $"""
                SELECT COUNT(1)
                FROM public."{tableName}"
                WHERE search_vector @@ plainto_tsquery('english', 'quick')
                """, commandTimeoutSeconds);

            Assert.That(matchCount, Is.EqualTo(1));
        }
        finally
        {
            await ExecuteNonQuery(connection, $"""DROP TABLE IF EXISTS public."{tableName}" CASCADE""", commandTimeoutSeconds, ignoreErrors: true);
        }
    }

    static async Task<int> ExecuteScalarInt(NpgsqlConnection connection, string sql, int commandTimeoutSeconds)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = sql;
        var scalar = await command.ExecuteScalarAsync();
        Assert.That(scalar, Is.Not.Null);
        return Convert.ToInt32(scalar);
    }

    static async Task ExecuteNonQuery(NpgsqlConnection connection, string sql, int commandTimeoutSeconds, bool ignoreErrors = false)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = commandTimeoutSeconds;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        catch when (ignoreErrors)
        {
        }
    }
}
