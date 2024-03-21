namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class ConnectionTestResults
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; set; }
    public ConnectionSettingsTestResult? AuditConnectionResult { get; set; }
    public ConnectionSettingsTestResult? MonitoringConnectionResult { get; set; }
    public ConnectionSettingsTestResult? BrokerConnectionResult { get; set; }
}

