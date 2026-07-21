namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

class FullTextSearchTests : PersistenceTestBase
{
    [Test]
    public async Task Can_create_and_query_full_text_index()
    {
        var sqlServerSettings = PersistenceSettings as SqlServerPersisterSettings;
        Assert.That(sqlServerSettings, Is.Not.Null);

        var tableName = $"fts_{Guid.NewGuid():N}";
        var catalogName = $"ftc_{Guid.NewGuid():N}";
        var primaryKeyName = $"pk_{tableName}";
        var commandTimeoutSeconds = 30;

        var connectionResult = await OpenConnectionForFullTextSearch(sqlServerSettings.ConnectionString, commandTimeoutSeconds);
        var temporaryDatabaseName = connectionResult.TemporaryDatabaseName;
        await using var connection = connectionResult.Connection;

        try
        {
            var isFullTextInstalled = await ExecuteScalarInt(connection, "SELECT FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')", commandTimeoutSeconds);
            Assert.That(isFullTextInstalled, Is.EqualTo(1), "SQL Server instance must have Full-Text Search installed.");

            await ExecuteNonQuery(connection, $"""
                CREATE TABLE [dbo].[{tableName}] (
                    [Id] INT IDENTITY(1,1) NOT NULL,
                    [Body] NVARCHAR(MAX) NOT NULL,
                    CONSTRAINT [{primaryKeyName}] PRIMARY KEY CLUSTERED ([Id])
                )
                """, commandTimeoutSeconds);

            await ExecuteNonQuery(connection, $"CREATE FULLTEXT CATALOG [{catalogName}]", commandTimeoutSeconds);

            await ExecuteNonQuery(connection, $"""
                CREATE FULLTEXT INDEX ON [dbo].[{tableName}]([Body] LANGUAGE 1033)
                KEY INDEX [{primaryKeyName}]
                ON [{catalogName}]
                WITH CHANGE_TRACKING AUTO
                """, commandTimeoutSeconds);

            await ExecuteNonQuery(connection, $"""
                INSERT INTO [dbo].[{tableName}]([Body]) VALUES
                (N'quick brown fox jumps'),
                (N'azure service bus transport')
                """, commandTimeoutSeconds);

            var found = await WaitForFullTextMatch(connection, tableName, commandTimeoutSeconds);
            Assert.That(found, Is.True);
        }
        finally
        {
            await ExecuteNonQuery(connection, $"""
                IF EXISTS (SELECT 1 FROM sys.fulltext_indexes fi
                           JOIN sys.tables t ON fi.object_id = t.object_id
                           WHERE t.name = '{tableName}')
                    DROP FULLTEXT INDEX ON [dbo].[{tableName}]
                """, commandTimeoutSeconds, ignoreErrors: true);

            await ExecuteNonQuery(connection, $"IF OBJECT_ID(N'[dbo].[{tableName}]', N'U') IS NOT NULL DROP TABLE [dbo].[{tableName}]", commandTimeoutSeconds, ignoreErrors: true);
            await ExecuteNonQuery(connection, $"IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{catalogName}') DROP FULLTEXT CATALOG [{catalogName}]", commandTimeoutSeconds, ignoreErrors: true);

            if (!string.IsNullOrEmpty(temporaryDatabaseName))
            {
                await connection.CloseAsync();
                await DropDatabase(sqlServerSettings.ConnectionString, temporaryDatabaseName, commandTimeoutSeconds);
            }
        }
    }

    static async Task<bool> WaitForFullTextMatch(SqlConnection connection, string tableName, int commandTimeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = commandTimeoutSeconds;
            command.CommandText = $"SELECT COUNT(1) FROM [dbo].[{tableName}] WHERE CONTAINS([Body], '\"quick\"')";
            var count = (int)await command.ExecuteScalarAsync();
            if (count == 1)
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }

    static async Task<int> ExecuteScalarInt(SqlConnection connection, string sql, int commandTimeoutSeconds)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = sql;
        var scalar = await command.ExecuteScalarAsync();
        Assert.That(scalar, Is.Not.Null);
        return Convert.ToInt32(scalar);
    }

    static async Task<string> ExecuteScalarString(SqlConnection connection, string sql, int commandTimeoutSeconds)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = commandTimeoutSeconds;
        command.CommandText = sql;
        var scalar = await command.ExecuteScalarAsync();
        Assert.That(scalar, Is.Not.Null);
        return (string)scalar;
    }

    static async Task ExecuteNonQuery(SqlConnection connection, string sql, int commandTimeoutSeconds, bool ignoreErrors = false)
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

    static async Task<(SqlConnection Connection, string TemporaryDatabaseName)> OpenConnectionForFullTextSearch(string connectionString, int commandTimeoutSeconds)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var databaseName = await ExecuteScalarString(connection, "SELECT DB_NAME()", commandTimeoutSeconds);
        if (!IsSystemDatabase(databaseName))
        {
            return (connection, string.Empty);
        }

        var temporaryDatabaseName = $"ftsdb_{Guid.NewGuid():N}";
        await ExecuteNonQuery(connection, $"CREATE DATABASE [{temporaryDatabaseName}]", commandTimeoutSeconds);
        await connection.DisposeAsync();

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = temporaryDatabaseName
        };

        var dedicatedConnection = new SqlConnection(builder.ConnectionString);
        await dedicatedConnection.OpenAsync();
        return (dedicatedConnection, temporaryDatabaseName);
    }

    static async Task DropDatabase(string connectionString, string databaseName, int commandTimeoutSeconds)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await ExecuteNonQuery(connection, $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """, commandTimeoutSeconds, ignoreErrors: true);
    }

    static bool IsSystemDatabase(string databaseName) =>
        string.Equals(databaseName, "master", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseName, "tempdb", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(databaseName, "model", StringComparison.OrdinalIgnoreCase);
}
