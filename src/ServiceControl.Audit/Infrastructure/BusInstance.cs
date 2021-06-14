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
            Bus = bus;
        }
        public IEndpointInstance Bus { get; }
        public AuditIngestionComponent AuditIngestion { get; }

        public Task Stop()
        {
            return Bus.Stop();
        }
    }
}