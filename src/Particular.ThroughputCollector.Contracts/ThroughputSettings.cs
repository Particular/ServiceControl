namespace Particular.ThroughputCollector.Contracts;

public class ThroughputSettings
{
    public ThroughputSettings(string serviceControlQueue, string errorQueue, string transportType, string customerName,
        string serviceControlVersion)
    {
        ServiceControlQueue = serviceControlQueue;
        ErrorQueue = errorQueue;
        TransportType = transportType;
        CustomerName = customerName;
        ServiceControlVersion = serviceControlVersion;
    }

    public string ErrorQueue { get; }
    public string ServiceControlQueue { get; }
    public string TransportType { get; set; }
    public string CustomerName { get; }
    public string ServiceControlVersion { get; }
}
