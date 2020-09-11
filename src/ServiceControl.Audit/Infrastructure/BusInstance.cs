namespace ServiceControl.Audit.Infrastructure
{
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;

    class BusInstance
    {
        public BusInstance(IEndpointInstance bus, AuditIngestionComponent auditIngestion)
        {
            AuditIngestion = auditIngestion;
            this.bus = bus;
        }

        public AuditIngestionComponent AuditIngestion { get; }

        public Task Stop()
        {
            return bus.Stop();
        }

        IEndpointInstance bus;
    }
}