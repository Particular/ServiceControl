namespace ServiceBus.Management.BusinessMonitoring
{
    using System;
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Operations.Heartbeats;

    public class EndpointHeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        public EndpointSLAMonitoring EndpointSLAMonitoring { get; set; }
        public IBus Bus { get; set; }

        public void Handle(EndpointHeartbeat message)
        {
            var endpoint = Bus.CurrentMessageContext.Headers[Headers.OriginatingEndpoint];

            if (message.Configuration.ContainsKey("Endpoint.SLA"))
            {
                EndpointSLAMonitoring.RegisterSLA(endpoint, TimeSpan.Parse(message.Configuration["Endpoint.SLA"]));
            }

            if (message.PerformanceData.ContainsKey("CriticalTime"))
            {
                EndpointSLAMonitoring.ReportCriticalTimeMeasurements(endpoint, message.PerformanceData["CriticalTime"]);
            }
        }
    }
}