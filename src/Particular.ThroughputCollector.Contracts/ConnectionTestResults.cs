namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class ConnectionTestResults(
    Broker broker,
    ConnectionSettingsTestResult auditConnectionResult,
    ConnectionSettingsTestResult monitoringConnectionResult,
    ConnectionSettingsTestResult brokerConnectionResult)
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; } = broker;

    public ConnectionSettingsTestResult AuditConnectionResult { get; } = auditConnectionResult;
    public ConnectionSettingsTestResult MonitoringConnectionResult { get; } = monitoringConnectionResult;
    public ConnectionSettingsTestResult BrokerConnectionResult { get; } = brokerConnectionResult;
}

