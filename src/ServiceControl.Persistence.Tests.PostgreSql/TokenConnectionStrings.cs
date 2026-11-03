namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

static class TokenConnectionStrings
{
    public const string CloudVariable = "ServiceControl_Persistence_PostgreSql_TokenConnectionString";

    public static string Cloud => Environment.GetEnvironmentVariable(CloudVariable);

    public static async Task<(string ConnectionString, string Password)> ForSharedContainer(string keywords, CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(await PostgreSqlSharedContainer.GetConnectionStringAsync(cancellationToken));
        var password = builder.Password ?? string.Empty;
        builder.Remove("Password");

        return ($"{builder.ConnectionString};{keywords}", password);
    }
}
