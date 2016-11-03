namespace ServiceControl.Infrastructure.SignalR
{
    using System.Linq;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;
    using NServiceBus.Unicast.Messages;

    public class GlobalEventHandler : IHandleMessages<IEvent>
    {
        static string[] emptyArray = new string[0];

        private readonly MessageMetadataRegistry registry;

        public GlobalEventHandler(MessageMetadataRegistry registry)
        {
            this.registry = registry;
        }

        public void Handle(IEvent @event)
        {
            var metadata = registry.GetMessageMetadata(@event.GetType());
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            context.Connection.Broadcast(new Envelope { Types = metadata.MessageHierarchy.Select(t=>t.Name).ToList(), Message = @event }, emptyArray)
                 .Wait();
        }
    }
}
