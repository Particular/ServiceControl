namespace ServiceControl.Alerts.SlaViolations
{
    using System;
    using NServiceBus;
    using ServiceControl.Contracts.BusinessMonitoring;

    public class EndpointSLABreachedHandler : IHandleMessages<EndpointSLABreached>
    {
        public void Handle(EndpointSLABreached message)
        {
            Console.Out.WriteLine("Demo - SLA breached for endpoint {0}", message.Endpoint);
        }
    }
}