namespace ServiceControl.Monitoring.Handler
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using NServiceBus;
    using ServiceControl.Persistence;

    [Handler]
    class RegisterNewEndpointHandler(IEndpointInstanceMonitoring endpointInstanceMonitoring) :
        IHandleMessages<NewEndpointDetected>,
        IHandleMessages<RegisterNewEndpoint>
    {
        // for backward compatibility reasons
        public Task Handle(NewEndpointDetected message, IMessageHandlerContext context) => endpointInstanceMonitoring.EndpointDetected(message.Endpoint, context.CancellationToken);

        public Task Handle(RegisterNewEndpoint message, IMessageHandlerContext context) => endpointInstanceMonitoring.EndpointDetected(message.Endpoint, context.CancellationToken);
    }
}