namespace ServiceControl.Infrastructure.SignalR
{
    using System.Threading;
    using System.Threading.Tasks;
    using DomainEvents;

    /// <summary>
    /// Forwards all domain events via SignalR
    /// </summary>
    class ServicePulseNotifier(GlobalEventHandler broadcaster) : IDomainHandler<IDomainEvent>
    {
        public async Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            if (domainEvent is IUserInterfaceEvent userInterfaceEvent)
            {
                await broadcaster.Broadcast(userInterfaceEvent, cancellationToken);
            }
        }
    }
}