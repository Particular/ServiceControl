namespace ServiceBus.Management.Alerts
{
    using System;
    using BusinessMonitoring;
    using NServiceBus;

    public class EndpointSLABreachedHandler : IHandleMessages<EndpointSLABreached>
    {
        public void Handle(EndpointSLABreached message)
        {
            Console.Out.WriteLine("Demo - SLA breached for endpoint {0}", message.Endpoint);
        }
    }
}