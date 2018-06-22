namespace ServiceBus.Management.Infrastructure
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class BusInstance
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