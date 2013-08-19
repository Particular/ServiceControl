namespace ServiceBus.Management.Infrastructure.Heartbeats
{
    using System;
    using NServiceBus;
    using ServiceControl.EndpointPlugins.Heartbeat;

    public class DisplayEndpointSLAHandler:IHandleMessages<EndpointHeartbeat>
    {
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            Console.Out.WriteLine("Got a heartbeat from {0} with SLA set to: {1}",
                Bus.CurrentMessageContext.Headers[Headers.OriginatingEndpoint],
                TimeSpan.Parse(message.Configuration["Endpoint.SLA"]));
        }
    }
}