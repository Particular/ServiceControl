namespace ServiceControl.Infrastructure.SignalR
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using NServiceBus.Unicast.Messages;
    using ServiceControl.Contracts.MessageFailures;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        private MessageMetadataRegistry messageMetadataRegistry;

        public GlobalEventHandler(MessageMetadataRegistry messageMetadataRegistry)
        {
            this.messageMetadataRegistry = messageMetadataRegistry;
        }

        public void Handle(IEvent @event)
        {
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            var failedMessagesImported = @event as FailedMessagesImported;
            if (failedMessagesImported != null)
            {
                HandleFailedMessagesImported(context.Connection, failedMessagesImported).Wait();
                return;
            }

            HandleGenericEvent(context.Connection, @event).Wait();
        }

        private Task HandleFailedMessagesImported(IConnection connection, FailedMessagesImported @event)
        {
            return Task.WhenAll(
                Dispatch(connection, from id in @event.NewFailureIds select new MessageFailed
                {
                    FailedMessageId = id
                }),
                Dispatch(connection, from id in @event.RepeatedFailureIds select new MessageFailedRepeatedly
                {
                    FailedMessageId = id
                })
            );
        }

        private Task HandleGenericEvent(IConnection connection, IEvent @event)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(@event.GetType());
            return connection.Broadcast(new Envelope
            {
                Types = metadata.MessageHierarchy.Select(t => t.Name).ToList(),
                Message = @event
            });
        }

        private Task Dispatch<T>(IConnection connection, IEnumerable<T> messages)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(typeof(T));
            var envelopeTypes = metadata.MessageHierarchy.Select(t => t.Name).ToList();

            return Task.WhenAll(from message in messages
                                select connection.Broadcast(new Envelope
                                {
                                    Types = envelopeTypes,
                                    Message = message
                                }));
        }

    }
}