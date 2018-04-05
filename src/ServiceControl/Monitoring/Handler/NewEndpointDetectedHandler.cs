namespace ServiceControl.Monitoring.Handler
{
    using NServiceBus;
    using ServiceControl.Contracts.EndpointControl;

    public class NewEndpointDetectedHandler : IHandleMessages<NewEndpointDetected>
    {
        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;

        public NewEndpointDetectedHandler(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public void Handle(NewEndpointDetected message)
        {
            var endpointInstanceId = new EndpointInstanceId(message.Endpoint.Name, message.Endpoint.Host, message.Endpoint.HostId);

            endpointInstanceMonitoring.GetOrCreateMonitor(endpointInstanceId, false);
        }
    }
}