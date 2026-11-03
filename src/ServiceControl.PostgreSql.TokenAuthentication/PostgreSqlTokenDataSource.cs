#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

static class PostgreSqlTokenDataSource
{
    // Asks for a token on every physical open: an AWS token stops working when the credentials that signed it expire, which can be minutes after signing.
    public static NpgsqlDataSource Build(string connectionString, IPostgreSqlTokenProvider tokenProvider) =>
        new NpgsqlDataSourceBuilder(connectionString)
            .UsePasswordProvider(
                settings => GetToken(tokenProvider, settings, CancellationToken.None).AsTask().GetAwaiter().GetResult(),
                (settings, cancellationToken) => GetToken(tokenProvider, settings, cancellationToken))
            .Build();

    public static string Describe(NpgsqlConnectionStringBuilder settings) =>
        settings.Database is null
            ? $"user {settings.Username} on host {settings.Host}"
            : $"user {settings.Username} on host {settings.Host}, database {settings.Database}";

    static async ValueTask<string> GetToken(IPostgreSqlTokenProvider tokenProvider, NpgsqlConnectionStringBuilder settings, CancellationToken cancellationToken)
    {
        try
        {
            return await tokenProvider.GetToken(settings, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Could not get a database access token for {Describe(settings)}.", e);
        }
    }
}
