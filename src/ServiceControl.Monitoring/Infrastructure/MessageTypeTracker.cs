namespace ServiceControl.Monitoring.Infrastructure
{
    using System.Threading.Tasks;
    using Messaging;
    using NServiceBus;

    [Handler]
    public class MessageTypeTracker(MessageTypeRegistry registry) : IHandleMessages<TaggedLongValueOccurrence>
    {
        public Task Handle(TaggedLongValueOccurrence message, IMessageHandlerContext context)
        {
            var endpointName = context.MessageHeaders[Headers.OriginatingEndpoint];

            registry.Record(new EndpointMessageType(endpointName, enclosedMessageTypes: message.TagValue));

            return Task.CompletedTask;
        }
    }
}