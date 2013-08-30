namespace ServiceControl.Infrastructure.SignalR
{
    using NServiceBus;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        public void Handle(IEvent @event)
        {
            this.Broadcast(@event);
        }
    }
}