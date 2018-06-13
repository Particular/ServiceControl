namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl.InternalMessages;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;

    class LegacyHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>,
        IHandleMessages<RegisterEndpoint>,
        IHandleMessages<EnableEndpointMonitoring>,
        IHandleMessages<DisableEndpointMonitoring>
    {
        public Task Handle(RegisterPotentiallyMissingHeartbeats message, IMessageHandlerContext context) => Task.FromResult(0);

        public Task Handle(RegisterEndpoint message, IMessageHandlerContext context) => Task.FromResult(0);

        public Task Handle(EnableEndpointMonitoring message, IMessageHandlerContext context) => Task.FromResult(0);

        public Task Handle(DisableEndpointMonitoring message, IMessageHandlerContext context) => Task.FromResult(0);
    }
}
