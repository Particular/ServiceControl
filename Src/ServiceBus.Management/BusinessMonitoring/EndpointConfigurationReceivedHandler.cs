namespace ServiceControl.BusinessMonitoring
{
    using System;
    using NServiceBus;
    using Contracts.Operations;

    public class EndpointConfigurationReceivedHandler : IHandleMessages<EndpointConfigurationReceived>
    {
        public EndpointSLAMonitoring EndpointSLAMonitoring { get; set; }

        public void Handle(EndpointConfigurationReceived message)
        {
            if (message.SettingsReceived.ContainsKey("Endpoint.SLA"))
            {
                EndpointSLAMonitoring.RegisterSLA(message.Endpoint, TimeSpan.Parse(message.SettingsReceived["Endpoint.SLA"]));
            }
        }
    }
}