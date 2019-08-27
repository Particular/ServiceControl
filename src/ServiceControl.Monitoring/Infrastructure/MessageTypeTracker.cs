namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Threading.Tasks;
    using Messaging;
    using NServiceBus;

    public class MessageTypeTracker : IHandleMessages<TaggedLongValueOccurrence>
    {
        readonly MessageTypeRegistry registry;

        public MessageTypeTracker(MessageTypeRegistry registry)
        {
            this.registry = registry;
        }

        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            registry.Record(new EndpointMessageType(endpointName, message.TagValue));

            return TaskEx.Completed;
        }
    }
}