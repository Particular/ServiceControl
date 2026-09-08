#:package Microsoft.Data.SqlClient@6.1.1

// Creates the ServiceControl test database if the service could not create it during provisioning,
// and fails the run if the server has no Full-Text Search. Message search is not optional, so an
// instance without it would otherwise fail every search test twenty minutes later with a much less
// obvious error.

using Microsoft.Data.SqlClient;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run verify-sqlserver.cs -- <connection-string>");
    return 1;
}

var builder = new SqlConnectionStringBuilder(args[0]);
var database = builder.InitialCatalog;

builder.InitialCatalog = "master";

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
}
catch (SqlException e)
{
    // Without the stack trace, which says nothing useful about a server that is unreachable or
    // rejecting the login.
    Console.Error.WriteLine($"Could not prepare {builder.DataSource}: {e.Message}");
    return 1;
}

Console.WriteLine($"Database '{database}' is present on {builder.DataSource} and Full-Text Search is installed.");
return 0;
