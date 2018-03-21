namespace Particular.HealthMonitoring.Uptime
{
    using Particular.Operations.Audits.Api;
    using ServiceControl.Api;

    class Output : ComponentOutput, IProvideAuditProcessor
    {
        public Output(IProcessAudits auditProcessor)
        {
            ProcessAudits = auditProcessor;
        }
        public IProcessAudits ProcessAudits { get; }
    }
}