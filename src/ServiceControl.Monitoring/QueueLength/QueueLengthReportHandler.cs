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
        public QueueLengthReportHandler(IProvideQueueLengthNew queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceIdDto.From(context.MessageHeaders);

            queueLengthProvider.Process(instanceId, new EndpointMetadataReportDto(message.LocalAddress));

            return TaskEx.Completed;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceIdDto.From(context.MessageHeaders);
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == QueueLengthMessageType)
            {
                queueLengthProvider.Process(instanceId, new TaggedLongValueOccurrenceDto(message.Entries.Select(e => ToEntry(e)).ToArray(), message.TagValue));
            }

            return TaskEx.Completed;
        }

         EntryDto ToEntry(RawMessage.Entry entry)
        {
            throw new System.NotImplementedException();
        }

        IProvideQueueLengthNew queueLengthProvider;

        const string QueueLengthMessageType = "QueueLength";
    }
}