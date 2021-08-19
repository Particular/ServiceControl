namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;

    public class QueueLengthReportHandler : IHandleMessages<TaggedLongValueOccurrence>
    {
        public QueueLengthReportHandler(QueueLengthStore queueLengthStore)
        {
            this.queueLengthStore = queueLengthStore;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == QueueLengthMessageType)
            {
                queueLengthStore.Store(message.Entries, new EndpointInputQueue(endpointName, message.TagValue));
            }

            return Task.CompletedTask;
        }


        QueueLengthStore queueLengthStore;

        const string QueueLengthMessageType = "QueueLength";
    }
}