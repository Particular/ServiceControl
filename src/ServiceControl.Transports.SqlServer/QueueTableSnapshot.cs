namespace ServiceControl.Transports.SqlServer;

public class BrokerQueueTableSnapshot(BrokerQueueTable details) : BrokerQueueTable(details.DatabaseDetails, details.Schema, details.Name)
{
    public long RowVersion { get; set; }
}