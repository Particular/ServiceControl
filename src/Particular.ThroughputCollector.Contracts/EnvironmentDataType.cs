namespace Particular.ThroughputCollector.Contracts;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnvironmentDataType
{
    AuditEnabled,
    MonitoringEnabled,
    Version,
    AuditInstances,
    ServiceControlVersion,
    ServicePulseVersion
}
