namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.CustomChecks;
    using Raven.Client;

    public class CustomCheckFailedPublisher : EventPublisher<CustomCheckFailed, CustomCheckFailedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(CustomCheckFailed @event)
        {
            return new DispatchContext
            {
                EndpointHost = @event.OriginatingEndpoint.Host,
                EndpointHostId = @event.OriginatingEndpoint.HostId,
                EndpointName = @event.OriginatingEndpoint.Name,
                FailedAt = @event.FailedAt,
                Category = @event.Category,
                FailureReason = @event.FailureReason,
                CustomCheckId = @event.CustomCheckId
            };
        }

        protected override Task<IEnumerable<object>> PublishEvents(IEnumerable<DispatchContext> contexts, IAsyncDocumentSession session)
        {
            return Task.FromResult(contexts.Select(r => (object)new Contracts.CustomCheckFailed
            {
                FailedAt = r.FailedAt,
                Category = r.Category,
                FailureReason = r.FailureReason,
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
            public string FailureReason { get; set; }
            public DateTime FailedAt { get; set; }
        }
    }
}