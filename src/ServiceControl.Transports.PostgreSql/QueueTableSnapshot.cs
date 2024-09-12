namespace ServiceControl.Transports.PostgreSql;

class BrokerQueueTableSnapshot(BrokerQueueTable details) : BrokerQueueTable(details.DatabaseDetails, details.QueueAddress)
{
    public long RowVersion { get; set; }
}