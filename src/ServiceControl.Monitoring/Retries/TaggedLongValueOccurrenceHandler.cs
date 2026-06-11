namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Infrastructure;
    using Messaging;
    using NServiceBus;

    [Handler]
    public class TaggedLongValueOccurrenceHandler(RetriesStore store) : IHandleMessages<TaggedLongValueOccurrence>
    {
        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var instanceId = EndpointInstanceId.From(context.MessageHeaders);
            var messageType = context.MessageHeaders[MetricHeaders.MetricType];

            if (messageType == RetriesMessageType)
            {
                store.Store(message.Entries, instanceId, new EndpointMessageType(instanceId.EndpointName, enclosedMessageTypes: message.TagValue));
            }

            return Task.CompletedTask;
        }

        const string RetriesMessageType = "Retries";
    }
}