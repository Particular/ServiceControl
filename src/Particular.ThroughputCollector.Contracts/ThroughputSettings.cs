namespace Particular.ThroughputCollector.Contracts;

public class ThroughputSettings
{
    public ThroughputSettings(Broker broker, string serviceControlQueue, string errorQueue, string persistenceType, string transportType, string customerName, string serviceControlVersion, List<string> auditQueues)
    {
        Broker = broker;
        ServiceControlQueue = serviceControlQueue;
        ErrorQueue = errorQueue;
        PersistenceType = persistenceType;
        TransportType = transportType;
        AuditQueues = auditQueues;
        CustomerName = customerName;
        ServiceControlVersion = serviceControlVersion;
    }

    public Broker Broker { get; set; }
    public string ErrorQueue { get; }
    public string ServiceControlQueue { get; }
    public List<string> AuditQueues { get; }
    public string PersistenceType { get; set; }
    public string TransportType { get; set; }
    public string CustomerName { get; }
    public string ServiceControlVersion { get; }
}
