namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;
    using NServiceBus;

    class DomainEventBusPublisher : IDomainHandler<IDomainEvent>
    {
        IMessageSession messageSession;

        public DomainEventBusPublisher(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

#pragma warning disable 1998
        public Task Handle(IDomainEvent domainEvent)
#pragma warning restore 1998
        {
            var busEvent = domainEvent as IBusEvent;

            if (busEvent != null)
            {
                return messageSession.Publish(busEvent);
            }
            return Task.FromResult(0);
        }
    }
}