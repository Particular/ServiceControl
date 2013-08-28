namespace Pulse.Messaging.Handlers
{
    using Messages;
    using NServiceBus;

    public class PingHandler : IHandleMessages<Ping>
    {
        public void Handle(Ping message)
        {
            this.Broadcast(new Pong {Message = "Hello " + message.Name + "!"});

        }
    }
}