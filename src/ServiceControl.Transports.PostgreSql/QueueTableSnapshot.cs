namespace ServiceControl.Transports.PostgreSql;

public class BrokerQueueTableSnapshot(BrokerQueueTable details) : BrokerQueueTable(details.DatabaseDetails, details.QueueAddress)
{
    public long RowVersion { get; set; }
}