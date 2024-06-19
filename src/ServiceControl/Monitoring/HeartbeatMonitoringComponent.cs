﻿namespace ServiceControl.Monitoring
{
    using Connection;
    using EndpointControl.Handlers;
    using EventLog;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using Transports;

    class HeartbeatMonitoringComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            hostBuilder.Services.AddHostedService<HeartbeatMonitoringHostedService>();
            hostBuilder.Services.AddSingleton<IEndpointInstanceMonitoring, EndpointInstanceMonitoring>();

            hostBuilder.Services.AddDomainEventHandler<MonitoringDataPersister>();

            hostBuilder.Services.AddEventLogMapping<EndpointFailedToHeartbeatDefinition>();
            hostBuilder.Services.AddEventLogMapping<HeartbeatingEndpointDetectedDefinition>();
            hostBuilder.Services.AddEventLogMapping<EndpointHeartbeatRestoredDefinition>();
            hostBuilder.Services.AddEventLogMapping<EndpointStartedDefinition>();
            hostBuilder.Services.AddEventLogMapping<KnownEndpointUpdatedDefinition>();

            hostBuilder.Services.AddIntegrationEventPublisher<HeartbeatRestoredPublisher>();
            hostBuilder.Services.AddIntegrationEventPublisher<HeartbeatStoppedPublisher>();

            hostBuilder.Services.AddErrorMessageEnricher<DetectNewEndpointsFromErrorImportsEnricher>();

            hostBuilder.Services.AddPlatformConnectionProvider<HeartbeatsPlatformConnectionDetailsProvider>();
        }
    }
}