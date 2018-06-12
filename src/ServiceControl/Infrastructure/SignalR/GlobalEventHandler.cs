namespace ServiceControl.Infrastructure.SignalR
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;

    public class GlobalEventHandler
    {
        static string[] emptyArray = new string[0];

        public Task Broadcast(object @event)
        {
            var typeName = @event.GetType().Name;
            var types = new List<string>
            {
                typeName
            };
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            return context.Connection.Broadcast(new Envelope { Types = types, Message = @event }, emptyArray);
        }
    }
}
