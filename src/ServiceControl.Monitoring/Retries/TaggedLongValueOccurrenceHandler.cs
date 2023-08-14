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
                var enclosedMessageTypes = message.TagValue;

                var index = enclosedMessageTypes.IndexOf(';');

                var firstType = index != -1
                    ? enclosedMessageTypes.Substring(0, index)
                    : enclosedMessageTypes;

                store.Store(message.Entries, instanceId, new EndpointMessageType(instanceId.EndpointName, firstType));
            }

            return Task.CompletedTask;
        }

        readonly RetriesStore store;
        const string RetriesMessageType = "Retries";
    }
}