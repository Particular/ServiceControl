#:package Npgsql@10.0.3

// Creates a login that signs in with an IAM token and can create its own schema in the test database. A role that is granted rds_iam can no longer sign in with a password.

using Npgsql;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: dotnet run create-iam-user-postgresql.cs -- <master-connection-string> <user>");
    return 1;
}

var builder = new NpgsqlConnectionStringBuilder(args[0]);
var user = args[1];

if (string.Equals(user, builder.Username, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"Refusing to grant rds_iam to '{user}', the user this script signs in as.");
    return 1;
}

await using var connection = new NpgsqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await using var command = connection.CreateCommand();
command.CommandText = $"""
    CREATE ROLE {QuoteIdentifier(user)} WITH LOGIN;
    GRANT rds_iam TO {QuoteIdentifier(user)};
    GRANT CREATE ON DATABASE {QuoteIdentifier(builder.Database!)} TO {QuoteIdentifier(user)};
    """;
await command.ExecuteNonQueryAsync();

Console.WriteLine($"Created '{user}' on {builder.Host}, which signs in to '{builder.Database}' with an IAM token.");
return 0;

static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
