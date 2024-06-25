namespace Particular.ServiceControl
{
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.EventLog;
    using global::ServiceControl.ExternalIntegrations;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Recoverability;

    static class ServiceControlMainInstance
    {
        public static readonly ServiceControlComponent[] Components = [
            new HostingComponent(),
            new EventLogComponent(),
            new ExternalIntegrationsComponent(),
            new RecoverabilityComponent(),
            new HeartbeatMonitoringComponent(),
            new CustomChecksComponent(),
            new LicensingComponent()
        ];
    }
}