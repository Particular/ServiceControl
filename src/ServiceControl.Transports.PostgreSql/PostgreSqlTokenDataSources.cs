#nullable enable
namespace ServiceControl.Transports.PostgreSql;

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Npgsql;
using ServiceControl.Infrastructure;
using ServiceControl.PostgreSql.TokenAuthentication;

/// <summary>
/// Shares one token data source per connection string, so the endpoints, the queue length provider and the throughput query of an instance refresh one token.
/// </summary>
sealed class PostgreSqlTokenDataSources(Func<PostgreSqlTokenAuthentication, IPostgreSqlTokenProvider> createTokenProvider)
{
    public PostgreSqlTokenDataSources() : this(authentication => authentication.CreateTokenProvider())
    {
    }

    public NpgsqlDataSource? GetOrCreate(string connectionString, string setting, out string npgsqlConnectionString)
    {
        var authentication = PostgreSqlTokenAuthentication.Parse(connectionString, setting, out npgsqlConnectionString);

        if (authentication is null)
        {
            return null;
        }

        var canonicalConnectionString = new NpgsqlConnectionStringBuilder(npgsqlConnectionString).ConnectionString;

        return dataSources.GetOrAdd((canonicalConnectionString, authentication), key => new Lazy<NpgsqlDataSource>(() => PostgreSqlTokenDataSource.Build(key.NpgsqlConnectionString, createTokenProvider(key.Authentication), logger))).Value;
    }

    // Npgsql's canonical form, so one connection written two ways, such as in the transport connection string and in PostgreSQL/ConnectionString, shares a data source.
    readonly ConcurrentDictionary<(string NpgsqlConnectionString, PostgreSqlTokenAuthentication Authentication), Lazy<NpgsqlDataSource>> dataSources = new();

    static readonly ILogger logger = LoggerUtil.CreateStaticLogger(typeof(PostgreSqlTokenDataSource));
}
