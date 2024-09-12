#nullable enable
namespace ServiceControl.Transports.PostgreSql;

using System.Collections.Generic;
using ServiceControl.Transports.BrokerThroughput;
class BrokerQueueTable(DatabaseDetails databaseDetails, QueueAddress queueAddress)
    : IBrokerQueue
{
    public DatabaseDetails DatabaseDetails { get; } = databaseDetails;
    public QueueAddress QueueAddress { get; } = queueAddress;
    public string SequenceName => $"{QueueAddress.Table}_seq_seq";
    public string QueueName => QueueAddress.QualifiedTableName;
    public string SanitizedName => QueueAddress.Table;
    public string? Scope => $"[{DatabaseDetails.DatabaseName}].[{QueueAddress.Schema}]";
    public List<string> EndpointIndicators { get; } = [];
}