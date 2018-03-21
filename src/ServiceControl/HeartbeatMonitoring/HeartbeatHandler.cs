namespace ServiceControl.Monitoring
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using Particular.Operations.Heartbeats.Api;
    using ServiceControl.Contracts.Operations;
    using EndpointHeartbeat = Plugin.Heartbeat.Messages.EndpointHeartbeat;

    class HeartbeatHandler : IHandleMessages<EndpointHeartbeat>
    {
        IEnumerable<IProcessHeartbeats> heartbeatProcessors;
        KnownEndpointsPersister persister;

        public HeartbeatHandler(IEnumerable<IProvideHeartbeatProcessor> components, KnownEndpointsPersister persister)
        {
            heartbeatProcessors = components.Select(c => c.ProcessHeartbeats).ToArray();
            this.persister = persister;
        }

        public void Handle(EndpointHeartbeat message)
        {
            foreach (var processor in heartbeatProcessors)
            {
                processor.Handle(new Particular.Operations.Heartbeats.Api.EndpointHeartbeat
                {
                    EndpointName = message.EndpointName,
                    ExecutedAt = message.ExecutedAt,
                    Host = message.Host,
                    HostId = message.HostId,
                }).GetAwaiter().GetResult();
            }
            
            persister.RegisterEndpoint(new EndpointDetails
            {
                Host = message.Host,
                HostId = message.HostId,
                Name =  message.EndpointName
            });
        }
    }
}
