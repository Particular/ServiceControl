namespace Particular.ThroughputCollector.Broker;

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Persistence;
using Shared;
using ThroughputQuery.SqlTransport;

public class SqlServerQuery(ILogger<SqlServerQuery> logger) : IThroughputQuery
{
    private readonly List<DatabaseDetails> databases = [];

    public void Initialise(FrozenDictionary<string, string> settings)
    {
        string connectionString = settings[SqlServerSettings.ConnectionString];
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

    public async IAsyncEnumerable<EndpointThroughput> GetThroughputPerDay(IQueueName queueName, DateTime startDate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Sampling queue table initial values...");
        var queueTableName = (QueueTableName)queueName;
        var startData =
            await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

        logger.LogInformation("Waiting to collect final values...");
        await Task.Delay(TimeSpan.FromHours(1), cancellationToken);

        logger.LogInformation("Sampling queue table final values...");
        var endData =
            await queueTableName.DatabaseDetails.GetSnapshot(queueTableName, cancellationToken);

        yield return new EndpointThroughput
        {
            Scope = queueTableName.GetScope(),
            DateUTC = DateTime.UtcNow.Date,
            TotalThroughput = endData.RowVersion - startData.RowVersion
        };
    }

    public async IAsyncEnumerable<IQueueName> GetQueueNames(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tables = new List<QueueTableName>();

        foreach (var db in databases)
        {
            await db.TestConnection(cancellationToken);
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
            switch (ScopeType)
            {
                case "Schema":
                    tableName.GetScope = () => tableName.Schema;
                    break;
                case "Catalog":
                    tableName.GetScope = () => tableName.DatabaseDetails.DatabaseName;
                    break;
                case "Catalog & Schema":
                    tableName.GetScope = () => tableName.DatabaseNameAndSchema;
                    break;
            }

            yield return tableName;
        }
    }

    public string? ScopeType { get; set; }
}