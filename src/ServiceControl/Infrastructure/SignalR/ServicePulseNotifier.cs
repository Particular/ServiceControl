namespace ServiceControl.Infrastructure.DomainEvents
{
    using NServiceBus;
    using ServiceControl.Infrastructure.SignalR;

    /// <summary>
    /// Forwards all domain events via SignalR
    /// </summary>
    public class ServicePulseNotifier : IDomainHandler<IEvent>
    {
        private readonly GlobalEventHandler broadcaster;

        public ServicePulseNotifier(GlobalEventHandler broadcaster)
        {
            this.broadcaster = broadcaster;
        }

        public void Handle(IEvent domainEvent)
        {
            broadcaster.Handle(domainEvent);
        }
    }
}