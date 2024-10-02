#nullable enable
namespace ServiceControl.Transports.PostgreSql;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrokerThroughput;
using Microsoft.Extensions.Logging;
using Npgsql;

public class PostgreSqlQuery(
    ILogger<PostgreSqlQuery> logger,
    TimeProvider timeProvider,
    TransportSettings transportSettings) : BrokerThroughputQuery(logger, "PostgreSql")
{
    readonly List<DatabaseDetails> databases = [];

    protected override void InitializeCore(ReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(PostgreSqlSettings.ConnectionString, out string? connectionString))
        {
            logger.LogInformation("Using ConnectionString used by instance");

            connectionString = transportSettings.ConnectionString.RemoveCustomConnectionStringParts(out string _, out string _);

            Diagnostics.AppendLine("ConnectionString not set, defaulted to using ConnectionString used by instance");
        }
        else
        {
            Diagnostics.AppendLine("ConnectionString set");
        }

        if (!settings.TryGetValue(PostgreSqlSettings.AdditionalDatabases, out string? additionalDatabases))
        {
            databases.Add(new DatabaseDetails(connectionString));
            Diagnostics.AppendLine("Additional databases not set");

            return;
        }

        Diagnostics.AppendLine(
            $"Additional databases set to {string.Join(", ", additionalDatabases.Split([' ', ',']).Select(s => $"\"{s}\""))}");

        var builder = new NpgsqlConnectionStringBuilder { ConnectionString = connectionString };

        foreach (string database in additionalDatabases.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries
                                                                      | StringSplitOptions.TrimEntries))
        {
            builder.Database = database;
            databases.Add(new DatabaseDetails(builder.ToString()));
        }
    }

    public override async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue,
        DateOnly startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queueTableName = (BrokerQueueTable)brokerQueue;
        var startData =
            await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

        // looping for 24 hours
        for (var i = 0; i < 24; i++)
        {
            await Task.Delay(TimeSpan.FromHours(1), timeProvider, cancellationToken);
            var endData =
                await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

            yield return new QueueThroughput
            {
                DateUTC = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime),
                TotalThroughput = endData.RowVersion - startData.RowVersion
            };

            startData = endData;
        }
    }

    public override async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tables = new List<BrokerQueueTable>();

        foreach (var db in databases)
        {
            var version = await db.TestConnection(cancellationToken);
            Data["SqlVersion"] = version;
            tables.AddRange(await db.GetTables(cancellationToken));
        }

        ScopeType = "Catalog & Schema";

        foreach (var tableName in tables)
        {
            yield return tableName;
        }
    }

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(PostgreSqlSettings.ConnectionString, PostgreSqlSettings.ConnectionStringDescription),
        new KeyDescriptionPair(PostgreSqlSettings.AdditionalDatabases, PostgreSqlSettings.AdditionalDatabasesDescription)
    ];

    protected override async Task<(bool Success, List<string> Errors)> TestConnectionCore(
        CancellationToken cancellationToken)
    {
        List<string> errors = [];

        foreach (DatabaseDetails db in databases)
        {
            try
            {
                await db.TestConnection(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Test connection failed");
                errors.Add(ex.Message);
            }
        }

        return (errors.Count == 0, errors);
    }

    public static class PostgreSqlSettings
    {
        public static readonly string ConnectionString = "PostgreSQL/ConnectionString";

        public static readonly string ConnectionStringDescription =
            "Database connection string that will provide at least read access to all queue tables.";
        public static readonly string AdditionalDatabases = "PostgreSQL/AdditionalDatabases";

        public static readonly string AdditionalDatabasesDescription =
            "When additional databases on the same server also contain NServiceBus message queues, the AdditionalDatabases setting specifies additional databases to search. The tool replaces the Database parameter in the connection string with the additional databases and queries all of them.";
    }
}