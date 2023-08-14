namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Threading.Tasks;
    using Messaging;
    using NServiceBus;

    public class MessageTypeTracker : IHandleMessages<TaggedLongValueOccurrence>
    {
        public MessageTypeTracker(MessageTypeRegistry registry)
        {
            this.registry = registry;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            var enclosedMessageTypes = message.TagValue;

            var index = enclosedMessageTypes.IndexOf(';');

            var firstType = index != -1
                ? enclosedMessageTypes.Substring(0, index)
                : enclosedMessageTypes;

            registry.Record(new EndpointMessageType(endpointName, firstType));

            return Task.CompletedTask;
        }

        readonly MessageTypeRegistry registry;
    }
}