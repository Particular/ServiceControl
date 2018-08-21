namespace ServiceControl.Monitoring.Handler
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using NServiceBus;

    public class NewEndpointDetectedHandler : IHandleMessages<NewEndpointDetected>
    {
        public NewEndpointDetectedHandler(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context)
        {
            endpointInstanceMonitoring.DetectEndpointFromRemoteAudit(message.Endpoint);
            return Task.FromResult(0);
        }

        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;
    }
}