namespace Particular.LicensingComponent.Contracts;

using ServiceControl.Configuration;

public class ThroughputSettings(string serviceControlQueue, string errorQueue, string transportType, string customerName, string serviceControlVersion)
{
    public static readonly SettingsRootNamespace SettingsNamespace = new("LicensingComponent");

    public const string DatabaseNameKey = "RavenDB/ThroughputDatabaseName";
    public const string DefaultDatabaseName = "throughput";

    public string ErrorQueue { get; } = errorQueue;
    public string ServiceControlQueue { get; } = serviceControlQueue;
    public string TransportType { get; set; } = transportType;
    public string CustomerName { get; } = customerName;
    public string ServiceControlVersion { get; } = serviceControlVersion;
}