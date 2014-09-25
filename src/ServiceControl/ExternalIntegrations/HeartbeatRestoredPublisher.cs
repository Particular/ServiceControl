namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    public class HeartbeatRestoredPublisher : EventPublisher<EndpointHeartbeatRestored, HeartbeatRestoredPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(EndpointHeartbeatRestored evnt)
        {
            return new DispatchContext
            {
                EndpointHost = evnt.Endpoint.Host,
                EndpointHostId = evnt.Endpoint.HostId,
                EndpointName = evnt.Endpoint.Name,
                RestoredAt = evnt.RestoredAt,
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            return contexts.Select(r => new HeartbeatRestored()
            {
                RestoredAt = r.RestoredAt,
                Host = r.EndpointHost,
                HostId = r.EndpointHostId,
                EndpointName = r.EndpointName
            });
        }

        public class DispatchContext
        {
            public string EndpointName { get; set; }
            public Guid EndpointHostId { get; set; }
            public string EndpointHost { get; set; }
            public DateTime RestoredAt { get; set; }
        }
    }
}