namespace Pulse.Messaging.Messages
{
    using NServiceBus;

    public class Ping : ICommand
    {
        public string Name { get; set; }
    }
}