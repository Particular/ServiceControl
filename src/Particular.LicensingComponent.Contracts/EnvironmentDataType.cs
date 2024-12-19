namespace Particular.LicensingComponent.Contracts;
using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnvironmentDataType
{
    AuditEnabled,
    MonitoringEnabled,
    BrokerVersion,
    AuditInstances,
    ServiceControlVersion,
    ServicePulseVersion
}