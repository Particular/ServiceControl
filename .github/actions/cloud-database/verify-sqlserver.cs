#:package Microsoft.Data.SqlClient@7.1.1

// Waits until the server accepts connections, creates the test database if the provisioning CLI
// could not, fails the run if the server has no Full-Text Search, and waits until a full-text
// query answers.

using Microsoft.Data.SqlClient;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run verify-sqlserver.cs -- <connection-string>");
    return 1;
}

var builder = new SqlConnectionStringBuilder(args[0]);
var database = builder.InitialCatalog;

builder.InitialCatalog = "master";

var deadline = DateTime.UtcNow.AddMinutes(10);
var attempt = 0;
SqlConnection master;

while (true)
{
    attempt++;

    try
    {
        master = new SqlConnection(builder.ConnectionString);
        await master.OpenAsync();
        break;
    }
    catch (SqlException e) when (DateTime.UtcNow < deadline)
    {
        Console.WriteLine($"{builder.DataSource} is not reachable yet (attempt {attempt}): {e.Message.Split('\n')[0]}");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
    catch (SqlException e)
    {
        Console.Error.WriteLine($"Gave up waiting for {builder.DataSource} after {attempt} attempts: {e.Message}");
        return 1;
    }
}

await using (master)
{
    // sys.databases rather than DB_ID: on Azure SQL the master database is a logical one, and
    // DB_ID returns null for a database that is sitting right there on the same server.
    bool exists;
    await using (var lookup = master.CreateCommand())
    {
        lookup.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @database";
        lookup.Parameters.AddWithValue("@database", database);
        exists = (int)(await lookup.ExecuteScalarAsync())! > 0;
    }

    if (!exists)
    {
        Console.WriteLine($"Creating database '{database}'.");
        await using var create = master.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{database}]";
        create.CommandTimeout = 300;
        await create.ExecuteNonQueryAsync();
    }

    await using (var fullText = master.CreateCommand())
    {
        fullText.CommandText = "SELECT CONVERT(int, ISNULL(SERVERPROPERTY('IsFullTextInstalled'), 0))";
        if ((int)(await fullText.ExecuteScalarAsync())! != 1)
        {
            Console.Error.WriteLine($"{builder.DataSource} does not have SQL Server Full-Text Search installed, which ServiceControl requires. On RDS, check that the edition supports it and that the option group includes it.");
            return 1;
        }
    }
}

Console.WriteLine($"Database '{database}' is present on {builder.DataSource} and Full-Text Search is installed, after {attempt} connection attempt(s).");

// A new Azure SQL database cannot reach its full-text daemon at first (error 30046), so the first
// searches of a run would fail. Waiting until a full-text query finds a row keeps that out of the tests.
var started = DateTime.UtcNow;
var fullTextDeadline = started.AddMinutes(5);
var queries = 0;

await using (var connection = new SqlConnection(args[0]))
{
    await connection.OpenAsync();

    await Execute(connection, "IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'sc_fulltext_warmup') CREATE FULLTEXT CATALOG sc_fulltext_warmup");
    await Execute(connection, "DROP TABLE IF EXISTS dbo.ScFullTextWarmup; CREATE TABLE dbo.ScFullTextWarmup (Id int NOT NULL CONSTRAINT PK_ScFullTextWarmup PRIMARY KEY, Body nvarchar(100) NOT NULL); INSERT INTO dbo.ScFullTextWarmup VALUES (1, N'warmup')");
    await Execute(connection, "CREATE FULLTEXT INDEX ON dbo.ScFullTextWarmup(Body LANGUAGE 0) KEY INDEX PK_ScFullTextWarmup ON sc_fulltext_warmup WITH (CHANGE_TRACKING AUTO)");

    while (true)
    {
        queries++;

        try
        {
            await using var search = connection.CreateCommand();
            search.CommandText = "SELECT COUNT(*) FROM dbo.ScFullTextWarmup WHERE CONTAINS(Body, 'warmup')";
            if ((int)(await search.ExecuteScalarAsync())! > 0)
            {
                break;
            }
        }
        catch (SqlException e) when (e.Number is 30046 or 30053)
        {
            Console.WriteLine($"Full-text search is not answering yet (query {queries}): {e.Message.Split('\n')[0]}");
        }

        if (DateTime.UtcNow > fullTextDeadline)
        {
            Console.Error.WriteLine($"A full-text query on {builder.DataSource} did not find its row within 5 minutes.");
            return 1;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    await Execute(connection, "DROP TABLE dbo.ScFullTextWarmup");
}

Console.WriteLine($"Full-text search answered after {(DateTime.UtcNow - started).TotalSeconds:0} s and {queries} queries.");
return 0;

static async Task Execute(SqlConnection connection, string sql)
{
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
}
