namespace ServiceControl.Infrastructure.SignalR
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using NServiceBus.Unicast.Messages;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        public MessageMetadataRegistry MessageMetadataRegistry { get; set; }

        public Task Handle(IEvent message, IMessageHandlerContext context)
        {
            var metadata = MessageMetadataRegistry.GetMessageMetadata(message.GetType());
            var connectionContext = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            return connectionContext.Connection.Broadcast(new Envelope
            {
                Types = metadata.MessageHierarchy.Select(t => t.Name).ToList(),
                Message = message
            });
        }
    }
}