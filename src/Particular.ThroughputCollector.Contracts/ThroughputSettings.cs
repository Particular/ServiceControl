namespace Particular.ThroughputCollector.Contracts;

using ServiceControl.Configuration;

public class ThroughputSettings
{
    public static readonly SettingsRootNamespace SettingsNamespace = new("ThroughputCollector");

    public ThroughputSettings(string serviceControlQueue, string errorQueue, string transportType, string customerName, string serviceControlVersion)
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
