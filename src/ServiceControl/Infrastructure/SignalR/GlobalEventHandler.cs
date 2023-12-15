namespace ServiceControl.Infrastructure.SignalR
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class GlobalEventHandler
    {
        public Task Broadcast(IUserInterfaceEvent @event)
        {
            var typeName = @event.GetType().Name;
            var types = new List<string>
            {
                typeName
            };
            var context = GlobalHost.ConnectionManager.GetConnectionContext<MessageStreamerConnection>();

            return context.Connection.Broadcast(new Envelope
            {
                Types = types,
                Message = @event
            }, emptyArray);
        }

        static string[] emptyArray = new string[0];
    }
}