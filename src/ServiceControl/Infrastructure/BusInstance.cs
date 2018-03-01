namespace ServiceBus.Management.Infrastructure
{
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    public class BusInstance
    {
        public BusInstance(IBus bus, IDomainEvents domainEvents)
        {
            Bus = bus;
            DomainEvents = domainEvents;
        }

        public IBus Bus { get; }
        public IDomainEvents DomainEvents { get; }
    }
}