namespace ServiceBus.Management.Infrastructure
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    public class BusInstance : IDisposable
    {
        public BusInstance(IBus bus, IDomainEvents domainEvents)
        {
            Bus = bus;
            DomainEvents = domainEvents;
        }

        public IBus Bus { get; }
        public IDomainEvents DomainEvents { get; }
        public void Dispose()
        {
            Bus.Dispose();
        }
    }
}