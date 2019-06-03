namespace ServiceControl.Monitoring.Handler
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using NServiceBus;

    class NewEndpointDetectedHandler : IHandleMessages<NewEndpointDetected>
    {
        public NewEndpointDetectedHandler(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context)
        {
            return endpointInstanceMonitoring.EndpointDetected(message.Endpoint);
        }

        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;
    }
}