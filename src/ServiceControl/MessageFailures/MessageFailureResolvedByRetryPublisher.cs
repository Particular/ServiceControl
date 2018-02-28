namespace ServiceControl.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure.DomainEvents;

    public class MessageFailureResolvedByRetryPublisher : IDomainHandler<MessageFailureResolvedByRetry>
    {
        IBus bus;

        public MessageFailureResolvedByRetryPublisher(IBus bus)
        {
            this.bus = bus;
        }

        public void Handle(MessageFailureResolvedByRetry domainEvent)
        {
            bus.Publish(domainEvent);
        }
    }
}