namespace Particular.ServiceControl
{
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.ExternalIntegrations;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Recoverability;
    using global::ServiceControl.SagaAudit;

    static class ServiceControlMainInstance
    {
        public static readonly ServiceControlComponent[] Components = {
            new HostingComponent(),
            new ExternalIntegrationsComponent(),
            new RecoverabilityComponent(),
            new SagaAuditComponent(),
            new HeartbeatMonitoringComponent(),
            new CustomChecksComponent()
        };
    }
}