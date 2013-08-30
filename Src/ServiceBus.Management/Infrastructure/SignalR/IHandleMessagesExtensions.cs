namespace ServiceControl.Infrastructure.SignalR
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;

    public static class IHandleMessagesExtensions
    {
        public static Task Broadcast<T>(this IHandleMessages<T> _, object message)
        {
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            return context.Connection.Broadcast(new Envelope { Type = message.GetType().Name, Message = message });
        }
    }
}