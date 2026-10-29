#nullable enable
namespace ServiceControl.PostgreSql.TokenAuthentication;

using System;
using Microsoft.Extensions.Logging;
using Npgsql;

static class PostgreSqlTokenDataSource
{
    public static NpgsqlDataSource Build(string connectionString, IPostgreSqlTokenProvider tokenProvider, ILogger logger) =>
        new NpgsqlDataSourceBuilder(connectionString)
            .UsePeriodicPasswordProvider(async (settings, cancellationToken) =>
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
                    logger.LogError(e, "Could not get a database access token. New connections keep using the previous token, if there is one, until it expires. The next attempt is in {RetryInterval}", RetryInterval);
                    throw;
                }
            }, RefreshInterval, RetryInterval)
            .Build();

    // Well inside the 15-minute lifetime of an AWS token, and few enough calls for AzureCliCredential, which starts a process each time.
    static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);
}
