namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Shared;
using ThroughputQuery.SqlTransport;

public class SqlServerQuery : IThroughputQuery, IBrokerInfo
{
    private readonly List<DatabaseDetails> databases = [];

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(SqlServerSettings.ConnectionString, out string? connectionString))
        {
            connectionString = settings[CommonSettings.TransportConnectionString];
        }
        if (!settings.TryGetValue(SqlServerSettings.AdditionalCatalogs, out string? catalogs))
        {
            databases.Add(new DatabaseDetails(connectionString));
            return;
        }

        var builder = new SqlConnectionStringBuilder { ConnectionString = connectionString };

        foreach (string catalog in catalogs.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries
                                                                      | StringSplitOptions.TrimEntries))
        {
            builder.InitialCatalog = catalog;
            databases.Add(new DatabaseDetails(builder.ToString()));
        }
    }

    public async IAsyncEnumerable<QueueThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var queueTableName = (QueueTableName)queueName;
        var startData =
            await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

        // looping for 24 hours
        for (int i = 0; i < 24; i++)
        {
            await Task.Delay(TimeSpan.FromHours(1), cancellationToken);

            var endData =
                await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

            yield return new QueueThroughput
            {
                Scope = queueTableName.GetScope(),
                DateUTC = DateTime.UtcNow.Date,
                TotalThroughput = endData.RowVersion - startData.RowVersion
            };

            startData = endData;
        }
    }

    public async IAsyncEnumerable<IQueueName> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tables = new List<QueueTableName>();

        foreach (var db in databases)
        {
            string version = await db.TestConnection(cancellationToken);
            Data["SqlVersion"] = version;
            tables.AddRange(await db.GetTables(cancellationToken));
        }

        int catalogCount = tables.Select(t => t.DatabaseDetails.DatabaseName).Distinct().Count();
        int schemaCount = tables.Select(t => $"{t.DatabaseDetails.DatabaseName}/{t.Schema}").Distinct().Count();

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
            tableName.GetScope = ScopeType switch
            {
                "Schema" => () => tableName.Schema,
                "Catalog" => () => tableName.DatabaseDetails.DatabaseName,
                "Catalog & Schema" => () => tableName.DatabaseNameAndSchema,
                _ => tableName.GetScope
            };

            yield return tableName;
        }
    }

    public string? ScopeType { get; set; }

    public bool SupportsHistoricalMetrics => false;
    public Dictionary<string, string> Data { get; } = [];
    public string MessageTransport => "RabbitMQ";
}