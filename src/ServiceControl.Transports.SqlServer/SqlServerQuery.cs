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
using Microsoft.Extensions.Logging;

public class SqlServerQuery(
    ILogger<SqlServerQuery> logger,
    TimeProvider timeProvider,
    TransportSettings transportSettings) : BrokerThroughputQuery(logger, "SqlTransport")
{
    readonly List<DatabaseDetails> databases = [];

    protected override void InitialiseCore(FrozenDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(SqlServerSettings.ConnectionString, out string? connectionString))
        {
            logger.LogInformation("Using ConnectionString used by instance");

            connectionString = transportSettings.ConnectionString;

            Diagnostics.AppendLine("ConnectionString not set, defaulted to using ConnectionString used by instance");
        }
        else
        {
            Diagnostics.AppendLine("ConnectionString set");
        }

        if (!settings.TryGetValue(SqlServerSettings.AdditionalCatalogs, out string? catalogs))
        {
            databases.Add(new DatabaseDetails(connectionString));
            Diagnostics.AppendLine("Additional catalogs not set");

            return;
        }

        Diagnostics.AppendLine(
            $"Additional catalogs set to {string.Join(", ", catalogs.Split([' ', ',']).Select(s => $"\"{s}\""))}");

        var builder = new SqlConnectionStringBuilder { ConnectionString = connectionString };

        foreach (string catalog in catalogs.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries
                                                                      | StringSplitOptions.TrimEntries))
        {
            builder.InitialCatalog = catalog;
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

    public override KeyDescriptionPair[] Settings =>
    [
        new KeyDescriptionPair(SqlServerSettings.ConnectionString, SqlServerSettings.ConnectionStringDescription),
        new KeyDescriptionPair(SqlServerSettings.AdditionalCatalogs, SqlServerSettings.AdditionalCatalogsDescription)
    ];

    public override async Task<(bool Success, List<string> Errors)> TestConnectionCore(
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

    public static class SqlServerSettings
    {
        public static readonly string ConnectionString = "SqlServer/ConnectionString";
        public static readonly string ConnectionStringDescription = "A single database connection string that will provide at least read access to all queue tables.";
        public static readonly string AdditionalCatalogs = "SqlServer/AdditionalCatalogs";
        public static readonly string AdditionalCatalogsDescription = "When the ConnectionString setting points to a single database, but multiple database catalogs on the same server also contain NServiceBus message queues, the AdditionalCatalogs setting specifies additional database catalogs to search. The tool replaces the Database or Initial Catalog parameter in the connection string with the additional catalog and queries all of them.";
    }
}