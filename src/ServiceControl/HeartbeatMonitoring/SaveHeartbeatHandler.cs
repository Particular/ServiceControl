namespace ServiceControl.HeartbeatMonitoring
{
    using System;
    using Infrastructure;
    using NServiceBus;
    using Plugin.Heartbeat.Messages;
    using Raven.Client;

    class SaveHeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public IDocumentSession Session { get; set; }
        public IBus Bus { get; set; }
        public HeartbeatStatusProvider HeartbeatStatusProvider { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            if (string.IsNullOrEmpty(message.EndpointName))
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the EndpointName in the schema");
            }

            if (string.IsNullOrEmpty(message.Host))
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the Host in the schema");
            }

            if (message.HostId == Guid.Empty)
            {
                throw new Exception("Received an EndpointHeartbeat message without proper initialization of the HostId in the schema");
            }
                

            var id = DeterministicGuid.MakeId(message.EndpointName, message.HostId.ToString());


            HeartbeatStatusProvider.UpdateHeartbeat(id, message);
        }
    }
}