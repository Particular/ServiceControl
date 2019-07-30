namespace ServiceControl.Monitoring.Handler
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using NServiceBus;

    class RegisterNewEndpointHandler : 
        IHandleMessages<NewEndpointDetected>,
        IHandleMessages<RegisterNewEndpoint>
    {
        public RegisterNewEndpointHandler(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            this.endpointInstanceMonitoring = endpointInstanceMonitoring;
        }

        // for backward compatibility reasons
        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context)
        {
            return endpointInstanceMonitoring.EndpointDetected(message.Endpoint);
        }

        public Task Handle(RegisterNewEndpoint message, IMessageHandlerContext context)
        {
            return endpointInstanceMonitoring.EndpointDetected(message.Endpoint);
        }

        readonly EndpointInstanceMonitoring endpointInstanceMonitoring;
    }
}