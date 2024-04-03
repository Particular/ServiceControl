#nullable enable
namespace ServiceControl.Transports.SqlServer;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

public class SqlServerQuery(TimeProvider timeProvider, TransportSettings transportSettings) : IThroughputQuery
{
    readonly List<DatabaseDetails> databases = [];

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(SqlServerSettings.ConnectionString, out var connectionString))
        {
            connectionString = transportSettings.ConnectionString;
        }
        if (!settings.TryGetValue(SqlServerSettings.AdditionalCatalogs, out var catalogs))
        {
            databases.Add(new DatabaseDetails(connectionString));
            return;
        }

        var builder = new SqlConnectionStringBuilder { ConnectionString = connectionString };

        foreach (var catalog in catalogs.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries
                                                                   | StringSplitOptions.TrimEntries))
        {
            builder.InitialCatalog = catalog;
            databases.Add(new DatabaseDetails(builder.ToString()));
        }
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IBrokerQueue brokerQueue, DateOnly startDate,
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

    public async IAsyncEnumerable<IBrokerQueue> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tables = new List<BrokerQueueTable>();

        foreach (var db in databases)
        {
            var version = await db.TestConnection(cancellationToken);
            Data["SqlVersion"] = version;
            tables.AddRange(await db.GetTables(cancellationToken));
        }

        var catalogCount = tables.Select(t => t.DatabaseDetails.DatabaseName).Distinct().Count();
        var schemaCount = tables.Select(t => $"{t.DatabaseDetails.DatabaseName}/{t.Schema}").Distinct().Count();

        if (catalogCount > 1)
        {
            ScopeType = schemaCount > 1 ? "Catalog & Schema" : "Catalog";
        }
        else if (schemaCount > 1)
        {
            ScopeType = "Schema";
        }

        foreach (var tableName in tables)
        {
            tableName.Scope = ScopeType switch
            {
                "Schema" => tableName.Schema,
                "Catalog" => tableName.DatabaseDetails.DatabaseName,
                "Catalog & Schema" => tableName.DatabaseNameAndSchema,
                _ => null
            };

            yield return tableName;
        }
    }

    public string? ScopeType { get; set; }

    public bool SupportsHistoricalMetrics => false;
    public KeyDescriptionPair[] Settings => [
        new KeyDescriptionPair(SqlServerSettings.ConnectionString, SqlServerSettings.ConnectionStringDescription),
        new KeyDescriptionPair(SqlServerSettings.AdditionalCatalogs, SqlServerSettings.AdditionalCatalogsDescription)
    ];
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "SqlTransport";

    public static class SqlServerSettings
    {
        public static readonly string ConnectionString = "SqlServer/ConnectionString";
        public static readonly string ConnectionStringDescription = "A single database connection string that will provide at least read access to all queue tables.";
        public static readonly string AdditionalCatalogs = "SqlServer/AdditionalCatalogs";
        public static readonly string AdditionalCatalogsDescription = "When the ConnectionString setting points to a single database, but multiple database catalogs on the same server also contain NServiceBus message queues, the AdditionalCatalogs setting specifies additional database catalogs to search. The tool replaces the Database or Initial Catalog parameter in the connection string with the additional catalog and queries all of them.";
    }
}