namespace ServiceControl.Infrastructure.SignalR
{
    using System.Linq;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using NServiceBus.Unicast.Messages;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        public MessageMetadataRegistry MessageMetadataRegistry { get; set; }

        public void Handle(IEvent @event)
        {
            var metadata = MessageMetadataRegistry.GetMessageDefinition(@event.GetType());
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            context.Connection.Broadcast(new Envelope { Types = metadata.MessageHierarchy.Select(t=>t.Name).ToList(), Message = @event })
                 .Wait();
        }
    }
}