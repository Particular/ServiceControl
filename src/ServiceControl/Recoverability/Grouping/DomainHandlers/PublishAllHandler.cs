namespace ServiceControl.Recoverability.Grouping.DomainHandlers
{
    using NServiceBus;
    using ServiceControl.Infrastructure.DomainEvents;

    public class PublishAllHandler : IDomainHandler<IEvent>
    {
        private readonly IBus bus;

        public PublishAllHandler(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(IEvent domainEvent)
        {
            bus.Publish(domainEvent);
        }
    }
}
