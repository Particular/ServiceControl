namespace Particular.LicensingComponent.Contracts;

public class ConnectionTestResults(
    string transport,
    ConnectionSettingsTestResult auditConnectionResult,
    ConnectionSettingsTestResult monitoringConnectionResult,
    ConnectionSettingsTestResult brokerConnectionResult)
{
    public string Transport { get; } = transport;

    public ConnectionSettingsTestResult AuditConnectionResult { get; } = auditConnectionResult;
    public ConnectionSettingsTestResult MonitoringConnectionResult { get; } = monitoringConnectionResult;
    public ConnectionSettingsTestResult BrokerConnectionResult { get; } = brokerConnectionResult;
}