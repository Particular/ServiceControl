﻿namespace ServiceControl.HeartbeatMonitoring
{
    using System.Threading.Tasks;
    using Audit.Monitoring;
    using EndpointControl.InternalMessages;
    using InternalMessages;
    using NServiceBus;

    class LegacyHandler : IHandleMessages<RegisterPotentiallyMissingHeartbeats>,
        IHandleMessages<RegisterEndpoint>,
        IHandleMessages<EnableEndpointMonitoring>,
        IHandleMessages<DisableEndpointMonitoring>
    {
        public Task Handle(DisableEndpointMonitoring message, IMessageHandlerContext context) => Task.CompletedTask;

        public Task Handle(EnableEndpointMonitoring message, IMessageHandlerContext context) => Task.CompletedTask;

        public Task Handle(RegisterEndpoint message, IMessageHandlerContext context) => Task.CompletedTask;
        public Task Handle(RegisterPotentiallyMissingHeartbeats message, IMessageHandlerContext context) => Task.CompletedTask;
    }
}