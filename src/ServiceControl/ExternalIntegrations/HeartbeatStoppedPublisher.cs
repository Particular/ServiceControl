namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;

    public class HeartbeatStoppedPublisher : EventPublisher<EndpointFailedToHeartbeat,HeartbeatStoppedPublisher.Reference>
    {
        protected override Reference CreateReference(EndpointFailedToHeartbeat evnt)
        {
            return new Reference
            {
                EndpointHost = evnt.Endpoint.Host,
                EndpointHostId = evnt.Endpoint.HostId,
                EndpointName = evnt.Endpoint.Name,
                DetectedAt = evnt.DetectedAt,
                LastReceivedAt = evnt.LastReceivedAt
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<Reference> references, IDocumentSession session)
        {
            return references.Select(r => new HeartbeatStopped
            {
                DetectedAt = r.DetectedAt,
                LastReceivedAt = r.LastReceivedAt,
                Endpoint = new HeartbeatStopped.EndpointInfo
                {
                    Host = r.EndpointHost,
                    HostId = r.EndpointHostId,
                    Name = r.EndpointName
                }
            });
        }

        public class Reference
        {
            public string EndpointName { get; set; }
            public Guid EndpointHostId { get; set; }
            public string EndpointHost { get; set; }
            public DateTime LastReceivedAt { get; set; }
            public DateTime DetectedAt { get; set; }
        }
    }
}