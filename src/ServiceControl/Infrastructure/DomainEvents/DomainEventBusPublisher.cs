namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;
    using NServiceBus;

    class DomainEventBusPublisher : IDomainHandler<IDomainEvent>
    {
        IBus bus;

        public DomainEventBusPublisher(IBus bus)
        {
            this.bus = bus;
        }

#pragma warning disable 1998
        public async Task Handle(IDomainEvent domainEvent)
#pragma warning restore 1998
        {
            if (domainEvent is IEvent)
            {
                bus.Publish(domainEvent);
            }
        }
    }
}