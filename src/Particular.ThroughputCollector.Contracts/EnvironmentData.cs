namespace Particular.ThroughputCollector.Contracts;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnvironmentData
{
    AuditEnabled,
    MonitoringEnabled,
    Version
}
