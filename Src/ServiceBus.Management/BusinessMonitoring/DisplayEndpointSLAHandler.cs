namespace ServiceBus.Management.BusinessMonitoring
{
    using System;
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Heartbeats;

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