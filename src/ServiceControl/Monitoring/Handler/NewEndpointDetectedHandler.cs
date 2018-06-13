namespace ServiceControl.Monitoring.Handler
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Contracts.EndpointControl;

    public class NewEndpointDetectedHandler : IHandleMessages<NewEndpointDetected>
    {
        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;

        public NewEndpointDetectedHandler(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context)
        {
            endpointInstanceMonitoring.DetectEndpointFromRemoteAudit(message.Endpoint);
            return Task.FromResult(0);
        }
    }
}