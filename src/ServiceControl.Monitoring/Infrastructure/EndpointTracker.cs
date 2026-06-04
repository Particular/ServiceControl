namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using Messaging;
    using NServiceBus;
    using NServiceBus.Metrics;

    [Handler]
    public class EndpointTracker(EndpointRegistry endpointRegistry, EndpointInstanceActivityTracker activityTracker) : IHandleMessages<MetricReport>, IHandleMessages<TaggedLongValueOccurrence>, IHandleMessages<EndpointMetadataReport>
    {
        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context) => RecordEndpointInstanceId(context);

        public Task Handle(MetricReport message, IMessageHandlerContext context) => RecordEndpointInstanceId(context);

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context) => RecordEndpointInstanceId(context);

        Task RecordEndpointInstanceId(IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);

            endpointRegistry.Record(instanceId);
            activityTracker.Record(instanceId, DateTime.UtcNow);

            return Task.CompletedTask;
        }
    }
}