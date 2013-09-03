namespace ServiceControl.Infrastructure.SignalR
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;

    public static class BusExtensions
    {
        public static Task Broadcast(this IBus _, object message)
        {
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            return context.Connection.Broadcast(new Envelope { Type = message.GetType().Name, Message = message });
        }
    }
}