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
            endpointInstanceMonitoring.DetectEndpointFromRemoteAudit(message.Endpoint);
        }
    }
}