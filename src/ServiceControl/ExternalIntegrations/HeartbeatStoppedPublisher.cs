namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    public class HeartbeatStoppedPublisher : EventPublisher<EndpointFailedToHeartbeat, HeartbeatStoppedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(EndpointFailedToHeartbeat @event)
        {
            return new DispatchContext
            {
                EndpointHost = @event.Endpoint.Host,
                EndpointHostId = @event.Endpoint.HostId,
                EndpointName = @event.Endpoint.Name,
                DetectedAt = @event.DetectedAt,
                LastReceivedAt = @event.LastReceivedAt
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new HeartbeatStopped
            {
                DetectedAt = r.DetectedAt,
                LastReceivedAt = r.LastReceivedAt,
                Host = r.EndpointHost,
                HostId = r.EndpointHostId,
                EndpointName = r.EndpointName
            }));
        }

        public class DispatchContext
        {
            public string EndpointName { get; set; }
            public Guid EndpointHostId { get; set; }
            public string EndpointHost { get; set; }
            public DateTime LastReceivedAt { get; set; }
            public DateTime DetectedAt { get; set; }
        }
    }
}