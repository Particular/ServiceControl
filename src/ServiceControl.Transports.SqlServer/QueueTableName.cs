#nullable enable
namespace ServiceControl.Transports.SqlServer;

using System.Collections.Generic;

public class BrokerQueueTable(DatabaseDetails databaseDetails, string tableSchema, string tableName)
    : IBrokerQueue
{
    public DatabaseDetails DatabaseDetails { get; } = databaseDetails;
    public string Schema { get; } = tableSchema;
    public string Name { get; } = tableName;

    public string FullName => $"[{Schema}].[{Name}]";

    public string QueueName => $"[{DatabaseDetails.DatabaseName}].{FullName}";
    public string? Scope { get; set; }
    public List<string> EndpointIndicators { get; } = [];

    public string DatabaseNameAndSchema => $"[{DatabaseDetails.DatabaseName}].[{Schema}]";

}