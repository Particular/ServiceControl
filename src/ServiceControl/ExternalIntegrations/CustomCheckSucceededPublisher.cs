namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Raven.Client;

    public class CustomCheckSucceededPublisher : EventPublisher<CustomCheckSucceeded, CustomCheckSucceededPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(CustomCheckSucceeded @event)
        {
            return new DispatchContext
            {
                EndpointHost = @event.OriginatingEndpoint.Host,
                EndpointHostId = @event.OriginatingEndpoint.HostId,
                EndpointName = @event.OriginatingEndpoint.Name,
                SucceededAt = @event.SucceededAt,
                Category = @event.Category,
                CustomCheckId = @event.CustomCheckId
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.CustomCheckSucceeded
            {
                SucceededAt = r.SucceededAt,
                Category = r.Category,
                CustomCheckId = r.CustomCheckId,
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
            public string CustomCheckId { get; set; }
            public string Category { get; set; }
            public DateTime SucceededAt { get; set; }
        }
    }
}