namespace ServiceControl.Transports.SqlServer;

public class QueueTableSnapshot(QueueTableName details) : QueueTableName(details.DatabaseDetails, details.Schema, details.Name)
{
    public long RowVersion { get; set; }
}