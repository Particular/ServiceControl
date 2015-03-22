namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client;
    using CustomCheckFailed = ServiceControl.Contracts.CustomCheckFailed;


    public class CustomCheckFailedPublisher : EventPublisher<Contracts.CustomChecks.CustomCheckFailed, CustomCheckFailedPublisher.DispatchContext>
    {
        protected override DispatchContext CreateDispatchRequest(Contracts.CustomChecks.CustomCheckFailed @event)
        {
            return new DispatchContext
            {
                EndpointHost = @event.OriginatingEndpoint.Host,
                //EndpointHostId = @event.OriginatingEndpoint.HostId, TODO
                EndpointName = @event.OriginatingEndpoint.Name,
                FailedAt = @event.FailedAt,
                Category = @event.Category,
                FailureReason = @event.FailureReason,
                CustomCheckId = @event.CustomCheckId,
            };
        }

        protected override IEnumerable<object> PublishEvents(IEnumerable<DispatchContext> contexts, IDocumentSession session)
        {
            return contexts.Select(r => new CustomCheckFailed
            {
                FailedAt = r.FailedAt,
                Category = r.Category,
                FailureReason = r.FailureReason,
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
            public string FailureReason { get; set; }
            public DateTime FailedAt { get; set; }
        }
    }
}