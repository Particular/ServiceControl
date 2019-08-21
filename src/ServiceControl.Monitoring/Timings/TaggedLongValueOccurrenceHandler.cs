namespace ServiceControl.Monitoring.Timings
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;

    public class TaggedLongValueOccurrenceHandler : IHandleMessages<TaggedLongValueOccurrence>
    {
        public TaggedLongValueOccurrenceHandler(ProcessingTimeStore processingTimeStore, CriticalTimeStore criticalTimeStore)
        {
            this.processingTimeStore = processingTimeStore;
            this.criticalTimeStore = criticalTimeStore;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);
            var messageType = new EndpointMessageType(instanceId.EndpointName, message.TagValue);

            var metricType = context.MessageHeaders[MetricHeaders.MetricType];

            switch (metricType)
            {
                case ProcessingTimeMessageType:
                    processingTimeStore.Store(message.Entries, instanceId, messageType);
                    break;
                case CriticalTimeMessageType:
                    criticalTimeStore.Store(message.Entries, instanceId, messageType);
                    break;
            }

            return TaskEx.Completed;
        }

        readonly ProcessingTimeStore processingTimeStore;
        readonly CriticalTimeStore criticalTimeStore;

        const string ProcessingTimeMessageType = "ProcessingTime";
        const string CriticalTimeMessageType = "CriticalTime";
    }
}