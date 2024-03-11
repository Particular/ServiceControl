namespace Particular.ThroughputQuery.SqlTransport;

using ThroughputCollector.Broker;

public class QueueTableName(DatabaseDetails databaseDetails, string tableSchema, string tableName)
    : IQueueName
{
    public DatabaseDetails DatabaseDetails { get; } = databaseDetails;
    public string Schema { get; } = tableSchema;
    public string Name { get; } = tableName;

    public string FullName => $"[{Schema}].[{Name}]";

    public string QueueName => $"[{DatabaseDetails.DatabaseName}].{FullName}";
}