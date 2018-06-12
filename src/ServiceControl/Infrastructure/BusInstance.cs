namespace ServiceBus.Management.Infrastructure
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    public class BusInstance : IDisposable
    {
        public BusInstance(IEndpointInstance bus, IDomainEvents domainEvents)
        {
            Bus = bus;
            DomainEvents = domainEvents;
        }

        public IEndpointInstance Bus { get; }
        public IDomainEvents DomainEvents { get; }
        public void Dispose()
        {
            Bus.Stop().GetAwaiter().GetResult();
        }
    }
}