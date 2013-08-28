namespace Pulse.Messaging.Messages
{
    using NServiceBus;

    public class Pong : ICommand
    {
        public string Message { get; set; }
    }
}