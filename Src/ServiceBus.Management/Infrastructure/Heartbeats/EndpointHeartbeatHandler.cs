namespace ServiceBus.Management.Infrastructure.Heartbeats
{
    using System;
    using NServiceBus;
    using ServiceControl.EndpointPlugins.Heartbeat;

    public class EndpointHeartbeatHandler:IHandleMessages<EndpointHeartbeat>
    {
        public void Handle(EndpointHeartbeat message)
        {
            Console.Out.WriteLine("Got a heartbeat");
        }
    }
}