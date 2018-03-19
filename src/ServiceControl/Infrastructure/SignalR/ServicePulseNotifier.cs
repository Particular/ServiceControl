namespace ServiceControl.Infrastructure.SignalR
{
    using ServiceControl.Infrastructure.DomainEvents;

    /// <summary>
    /// Forwards all domain events via SignalR
    /// </summary>
    public class ServicePulseNotifier : IDomainHandler<IDomainEvent>
    {
        GlobalEventHandler broadcaster;

        public ServicePulseNotifier(GlobalEventHandler broadcaster)
        {
            this.broadcaster = broadcaster;
        }

        public void Handle(IDomainEvent domainEvent)
        {
            var userInteraceEvent = domainEvent as IUserInterfaceEvent;
            if (userInteraceEvent != null)
            {
                broadcaster.Broadcast(userInteraceEvent);
            }
        }
    }
}