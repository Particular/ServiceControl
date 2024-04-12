namespace Particular.ThroughputCollector.Contracts;

public class ThroughputSettings
{
    public ThroughputSettings(string serviceControlQueue, string errorQueue, string persistenceType, string transportType, string customerName, string serviceControlVersion)
    {
        ServiceControlQueue = serviceControlQueue;
        ErrorQueue = errorQueue;
        PersistenceType = persistenceType;
        TransportType = transportType;
        CustomerName = customerName;
        ServiceControlVersion = serviceControlVersion;
    }

    public string ErrorQueue { get; }
    public string ServiceControlQueue { get; }
    public string PersistenceType { get; set; }
    public string TransportType { get; set; }
    public string CustomerName { get; }
    public string ServiceControlVersion { get; }
}
