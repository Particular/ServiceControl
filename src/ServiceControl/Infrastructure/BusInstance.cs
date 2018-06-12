namespace ServiceBus.Management.Infrastructure
{
    using System;
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations;

    public class BusInstance : IDisposable
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
        
        public void Dispose()
        {
            Bus.Stop().GetAwaiter().GetResult();
        }
    }
}