namespace ServiceControl.Infrastructure.SignalR
{
    using NServiceBus;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        public IBus Bus { get; set; }

        public void Handle(IEvent @event)
        {
            Bus.Broadcast(@event);
        }
    }
}