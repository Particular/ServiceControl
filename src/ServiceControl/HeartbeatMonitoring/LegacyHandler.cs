namespace ServiceControl.HeartbeatMonitoring
{
    using NServiceBus;
    using ServiceControl.CompositeViews.Endpoints;
    using ServiceControl.EndpointControl.InternalMessages;
    using ServiceControl.HeartbeatMonitoring.InternalMessages;

    class LegacyHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>,
        IHandleMessages<RegisterEndpoint>,
        IHandleMessages<EnableEndpointMonitoring>,
        IHandleMessages<DisableEndpointMonitoring>
    {
        public void Handle(RegisterPotentiallyMissingHeartbeats message) { }

        public void Handle(RegisterEndpoint message) { }

        public void Handle(EnableEndpointMonitoring message) { }

        public void Handle(DisableEndpointMonitoring message) { }
    }
}
