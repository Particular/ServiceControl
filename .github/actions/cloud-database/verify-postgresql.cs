#:package Npgsql@10.0.3

// Waits until the server actually accepts connections on the test database.

using Npgsql;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run verify-postgresql.cs -- <connection-string>");
    return 1;
}

var builder = new NpgsqlConnectionStringBuilder(args[0]);
var deadline = DateTime.UtcNow.AddMinutes(10);
var attempt = 0;

while (true)
{
    attempt++;

    try
    {
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_database()";
        var database = (string)(await command.ExecuteScalarAsync())!;

        Console.WriteLine($"Database '{database}' is reachable on {builder.Host}, after {attempt} attempt(s).");
        return 0;
    }
    catch (Exception e) when (e is NpgsqlException or TimeoutException && DateTime.UtcNow < deadline)
    {
        Console.WriteLine($"{builder.Host} is not ready yet (attempt {attempt}): {e.Message.Split('\n')[0]}");
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
    catch (Exception e) when (e is NpgsqlException or TimeoutException)
    {
        Console.Error.WriteLine($"Gave up waiting for {builder.Host} after {attempt} attempts: {e.Message}");
        return 1;
    }
}
