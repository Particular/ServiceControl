namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using ServiceControl.Contracts;

    public class CustomCheckSucceededPublisher : EventPublisher<Contracts.CustomChecks.CustomCheckSucceeded, CustomCheckSucceededPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(Contracts.CustomChecks.CustomCheckSucceeded @event)
        {
            return new DispatchContext
            {
                EndpointHost = @event.OriginatingEndpoint.Host,
                //EndpointHostId = @event.OriginatingEndpoint.HostId, TODO
                EndpointName = @event.OriginatingEndpoint.Name,
                SucceededAt = @event.SucceededAt,
                Category = @event.Category,
                CustomCheckId = @event.CustomCheckId,
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            return contexts.Select(r => new CustomCheckSucceeded
            {
                SucceededAt = r.SucceededAt,
                Category = r.Category,
                CustomCheckId = r.CustomCheckId,
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
            public string CustomCheckId { get; set; }
            public string Category { get; set; }
            public DateTime SucceededAt { get; set; }
        }
    }
}