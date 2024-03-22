namespace ServiceControl.Transports.SqlServer;

using System;

public class QueueTableName(DatabaseDetails databaseDetails, string tableSchema, string tableName)
    : IQueueName
{
    public Func<string> GetScope { get; set; } = () => null;
    public DatabaseDetails DatabaseDetails { get; } = databaseDetails;
    public string Schema { get; } = tableSchema;
    public string Name { get; } = tableName;

    public string FullName => $"[{Schema}].[{Name}]";

    public string QueueName => $"[{DatabaseDetails.DatabaseName}].{FullName}";

    public string DatabaseNameAndSchema => $"[{DatabaseDetails.DatabaseName}].[{Schema}]";

}