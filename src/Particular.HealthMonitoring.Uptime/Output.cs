namespace Particular.HealthMonitoring.Uptime
{
    using Nancy;
    using Particular.Operations.Audits.Api;
    using Particular.Operations.Errors.Api;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.Api;
    using ServiceControl.Infrastructure.DomainEvents;

    class Output : ComponentOutput, 
        IProvideAuditProcessor,
        IProvideErrorProcessor,
        IProvideHeartbeatProcessor,
        IProvideNancyModule,
        IProvideStartable
    {
        public Output(IProcessAudits auditProcessor, IProcessErrors processErrors, IProcessHeartbeats processHeartbeats, INancyModule nancyModule, IStartable startable)
        {
            ProcessAudits = auditProcessor;
            ProcessErrors = processErrors;
            ProcessHeartbeats = processHeartbeats;
            NancyModule = nancyModule;
            Startable = startable;
        }
        public IProcessAudits ProcessAudits { get; }
        public IProcessErrors ProcessErrors { get; }
        public IProcessHeartbeats ProcessHeartbeats { get; }
        public INancyModule NancyModule { get; }
        public IStartable Startable { get; }
    }
}