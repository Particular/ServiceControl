namespace ServiceControl.Infrastructure.SignalR
{
    using System.Linq;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using NServiceBus.Unicast.Messages;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        public SignalrIsReady SignalrIsReady { get; set; }

        public MessageMetadataRegistry MessageMetadataRegistry { get; set; }

        public IBus Bus { get; set; }

        public void Handle(IEvent @event)
        {
            if (!SignalrIsReady.Ready)
            {
                Bus.HandleCurrentMessageLater();
                return;
            }

            var metadata = MessageMetadataRegistry.GetMessageMetadata(@event.GetType());
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            context.Connection.Broadcast(new Envelope { Types = metadata.MessageHierarchy.Select(t=>t.Name).ToList(), Message = @event })
                 .Wait();
        }
    }

    public class SignalrIsReady
    {
        public bool Ready { get; set; }
    }
}