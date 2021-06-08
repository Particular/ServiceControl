namespace Particular.ServiceControl
{
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Recoverability;
    using global::ServiceControl.SagaAudit;

    class ServiceControlMainInstance
    {
        public static readonly ServiceControlComponent[] Components = {
            new HostingComponent(),
            new RecoverabilityComponent(),
            new SagaAuditComponent(),
            new HeartbeatMonitoringComponent(),
            new CustomChecksComponent()
        };
    }
}