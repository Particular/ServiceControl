namespace Particular.ThroughputCollector.Contracts;

public class ThroughputSettings
{
    public ThroughputSettings(Broker broker, string serviceControlQueue, string errorQueue, string persistenceType, string customerName, string serviceControlVersion, string auditQueue = "audit")
    {
        Broker = broker;
        ServiceControlQueue = serviceControlQueue;
        ErrorQueue = errorQueue;
        PersistenceType = persistenceType;
        AuditQueue = auditQueue;
        CustomerName = customerName;
        ServiceControlVersion = serviceControlVersion;
    }

    public Broker Broker { get; set; }
    public string ErrorQueue { get; }
    public string ServiceControlQueue { get; }
    public string AuditQueue { get; } //NOTE can we get this?
    public string PersistenceType { get; set; }
    public string CustomerName { get; }
    public string ServiceControlVersion { get; }
}
