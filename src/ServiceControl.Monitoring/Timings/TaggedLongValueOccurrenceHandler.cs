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

            var metricType = context.MessageHeaders[MetricHeaders.MetricType];

            var enclosedMessageTypes = message.TagValue;

            var index = enclosedMessageTypes.IndexOf(';');

            var firstType = index != -1
                ? enclosedMessageTypes.Substring(0, index)
                : enclosedMessageTypes;

            var messageType = new EndpointMessageType(instanceId.EndpointName, firstType);

            switch (metricType)
            {
                case ProcessingTimeMessageType:
                    processingTimeStore.Store(message.Entries, instanceId, messageType);
                    break;
                case CriticalTimeMessageType:
                    criticalTimeStore.Store(message.Entries, instanceId, messageType);
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }

        readonly ProcessingTimeStore processingTimeStore;
        readonly CriticalTimeStore criticalTimeStore;

        const string ProcessingTimeMessageType = "ProcessingTime";
        const string CriticalTimeMessageType = "CriticalTime";
    }
}