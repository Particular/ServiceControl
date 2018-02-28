namespace ServiceControl.Operations
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure.DomainEvents;

    class MessageFailedPublisher : IDomainHandler<MessageFailed>
    {
        IBus bus;

        public MessageFailedPublisher(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(MessageFailed domainEvent)
        {
            bus.Publish(domainEvent);
        }
    }
}