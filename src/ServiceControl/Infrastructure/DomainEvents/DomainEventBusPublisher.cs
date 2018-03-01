namespace ServiceControl.Infrastructure.DomainEvents
{
    using NServiceBus;

    class DomainEventBusPublisher : IDomainHandler<IDomainEvent>
    {
        IBus bus;

        public DomainEventBusPublisher(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(IDomainEvent domainEvent)
        {
            if (domainEvent is IEvent)
            {
                bus.Publish(domainEvent);
            }
        }
    }
}