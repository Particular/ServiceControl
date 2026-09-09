#:package Microsoft.Data.SqlClient@6.1.1

// Waits until the server actually accepts connections, creates the test database if the service
// could not create it during provisioning, and fails the run if the server has no Full-Text Search.
//
// The provisioning CLIs report a database as created before it is necessarily reachable, and an
// Azure firewall rule takes a moment to propagate, so this retries rather than taking the first
// refusal as final. Message search is not optional either, so a server without Full-Text Search is
// better caught here than twenty minutes later in the search tests.

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

while (true)
{
    attempt++;

    try
    {
        await using var master = new SqlConnection(builder.ConnectionString);
        await master.OpenAsync();

        await using (var create = master.CreateCommand())
        {
            create.CommandText = $"IF DB_ID(N'{database}') IS NULL CREATE DATABASE [{database}]";
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

        Console.WriteLine($"Database '{database}' is present on {builder.DataSource} and Full-Text Search is installed, after {attempt} attempt(s).");
        return 0;
    }
    catch (SqlException e) when (DateTime.UtcNow < deadline)
    {
        Console.WriteLine($"{builder.DataSource} is not ready yet (attempt {attempt}): {e.Message.Split('\n')[0]}");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
    catch (SqlException e)
    {
        Console.Error.WriteLine($"Gave up waiting for {builder.DataSource} after {attempt} attempts: {e.Message}");
        return 1;
    }
}
