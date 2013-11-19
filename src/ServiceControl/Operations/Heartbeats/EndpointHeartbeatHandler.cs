namespace ServiceControl.Operations.Heartbeats
{
    using Contracts.Operations;
    using EndpointPlugin.Messages.Heartbeats;
    using NServiceBus;

    public class EndpointHeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            var endpoint = Bus.CurrentMessageContext.Headers[Headers.OriginatingEndpoint];

            Bus.InMemory.Raise(new EndpointHeartbeatReceived
            {
                Endpoint = endpoint,
                Machine = Bus.CurrentMessageContext.Headers[Headers.OriginatingMachine],
                SentAt = message.ExecutedAt,
            });
        }
    }
}