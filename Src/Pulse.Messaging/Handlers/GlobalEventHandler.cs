namespace Pulse.Messaging.Handlers
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