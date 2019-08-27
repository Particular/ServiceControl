namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;
    using NServiceBus.Metrics;

    public class QueueLengthReportHandler : IHandleMessages<EndpointMetadataReport>, IHandleMessages<TaggedLongValueOccurrence>
    {
        IProvideQueueLength queueLengthProvider;

        public QueueLengthReportHandler(IProvideQueueLength queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        public Task Handle(EndpointMetadataReport message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);

            queueLengthProvider.Process(instanceId, message);

            return TaskEx.Completed;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == QueueLengthMessageType)
            {
                queueLengthProvider.Process(instanceId, message);
            }

            return TaskEx.Completed;
        }

        const string QueueLengthMessageType = "QueueLength";
    }
}