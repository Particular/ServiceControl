namespace ServiceBus.Management.Infrastructure
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Infrastructure.DomainEvents;

    class BusInstance
    {
        public BusInstance(IEndpointInstance bus, IDomainEvents domainEvents, ImportFailedAudits importFailedAudits)
        {
            ImportFailedAudits = importFailedAudits;
            Bus = bus;
            DomainEvents = domainEvents;
        }

        public IEndpointInstance Bus { get; }
        public IDomainEvents DomainEvents { get; }
        public ImportFailedAudits ImportFailedAudits { get; }

        public Task Stop()
        {
            return Bus.Stop();
        }
    }
}