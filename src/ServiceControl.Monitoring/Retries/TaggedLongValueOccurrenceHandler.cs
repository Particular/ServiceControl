namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;

    public class TaggedLongValueOccurrenceHandler : IHandleMessages<TaggedLongValueOccurrence>
    {
        public TaggedLongValueOccurrenceHandler(RetriesStore store)
        {
            this.store = store;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == RetriesMessageType)
            {
                store.Store(message.Entries, instanceId, new EndpointMessageType(instanceId.EndpointName, message.TagValue));
            }

            return TaskEx.Completed;
        }

        readonly RetriesStore store;
        const string RetriesMessageType = "Retries";
    }
}