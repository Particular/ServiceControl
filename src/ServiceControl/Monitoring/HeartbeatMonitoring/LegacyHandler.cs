namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using CompositeViews.Endpoints;
    using EndpointControl.InternalMessages;
    using InternalMessages;
    using NServiceBus;

    class LegacyHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>,
        IHandleMessages<RegisterEndpoint>,
        IHandleMessages<EnableEndpointMonitoring>,
        IHandleMessages<DisableEndpointMonitoring>
    {
        public Task Handle(DisableEndpointMonitoring message, IMessageHandlerContext context) => Task.FromResult(0);

        public Task Handle(EnableEndpointMonitoring message, IMessageHandlerContext context) => Task.FromResult(0);

        public Task Handle(RegisterEndpoint message, IMessageHandlerContext context) => Task.FromResult(0);
        public Task Handle(RegisterPotentiallyMissingHeartbeats message, IMessageHandlerContext context) => Task.FromResult(0);
    }
}