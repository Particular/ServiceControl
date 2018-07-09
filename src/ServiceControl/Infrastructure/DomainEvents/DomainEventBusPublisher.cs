namespace ServiceControl.Infrastructure.DomainEvents
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;

    public class DomainEventBusPublisher : FeatureStartupTask, IDomainHandler<IDomainEvent>
    {
        IMessageSession messageSession;

#pragma warning disable 1998
        public Task Handle(IDomainEvent domainEvent)
#pragma warning restore 1998
        {
            var busEvent = domainEvent as IBusEvent;

            if (busEvent != null)
            {
                return messageSession.Publish(busEvent);
            }

            var busCommand = domainEvent as IMessage;
            if (busCommand != null)
            {
                return messageSession.SendLocal(busCommand);
            }

            return Task.FromResult(0);
        }

        protected override Task OnStart(IMessageSession session)
        {
            messageSession = session;
            return Task.FromResult(true);
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.FromResult(true);
        }
    }
}