#:package Microsoft.Data.SqlClient@6.1.1

// Waits until the server accepts connections, creates the test database if the provisioning CLI
// could not, and fails the run if the server has no Full-Text Search.

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
return 0;
