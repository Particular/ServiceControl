namespace ServiceControl.Monitoring.QueueLength
{
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;
    using NServiceBus.Metrics;
    using Transports;

    public class QueueLengthReportHandler : IHandleMessages<EndpointMetadataReport>, IHandleMessages<TaggedLongValueOccurrence>
    {
        public QueueLengthReportHandler(IProvideQueueLength queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            queueLengthProvider.TrackEndpointInputQueue(endpointName, message.LocalAddress);

            return TaskEx.Completed;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == QueueLengthMessageType)
            {
                queueLengthProvider.Process(endpointName, new TaggedLongValueOccurrenceDto(message.Entries.Select(e => ToEntry(e)).ToArray(), message.TagValue));
            }

            return TaskEx.Completed;
        }

        EntryDto ToEntry(RawMessage.Entry entry)
        {
            return new EntryDto
            {
                DateTicks = entry.DateTicks,
                Value = entry.Value
            };
        }

        IProvideQueueLength queueLengthProvider;

        const string QueueLengthMessageType = "QueueLength";
    }
}