namespace ServiceControl.HeartbeatMonitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Infrastructure.DomainEvents;
    using RegisterEndpointStartup = ServiceControl.Plugin.Heartbeat.Messages.RegisterEndpointStartup;

    class RegisterEndpointStartupHandler : IHandleMessages<RegisterEndpointStartup>
    {
        IEnumerable<IProcessHeartbeats> heartbeatProcessors;
        IDomainEvents domainEvents;

        public RegisterEndpointStartupHandler(IDomainEvents domainEvents, IEnumerable<IProvideHeartbeatProcessor> components)
        {
            this.domainEvents = domainEvents;
            heartbeatProcessors = components.Select(c => c.ProcessHeartbeats).ToArray();
        }

        public void Handle(RegisterEndpointStartup message)
        {
            foreach (var processor in heartbeatProcessors)
            {
                processor.Handle(new Particular.Operations.Heartbeats.Api.RegisterEndpointStartup
                {
                    Endpoint = message.Endpoint,
                    HostId = message.HostId,
                    Host = message.Host,
                    HostDisplayName = message.HostDisplayName,
                    HostProperties = message.HostProperties,
                    StartedAt = message.StartedAt
                }).GetAwaiter().GetResult();
            }

            domainEvents.Raise(new EndpointStarted
            {
                EndpointDetails = new EndpointDetails
                {
                    Host = message.Host,
                    HostId = message.HostId,
                    Name = message.Endpoint
                },
                StartedAt = message.StartedAt
            });
        }
    }
}