namespace ServiceControl.Infrastructure.SignalR
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;

    class GlobalEventHandler(IHubContext<MessageStreamerHub> hubContext)
    {
        public Task Broadcast(IUserInterfaceEvent @event)
        {
            var typeName = @event.GetType().Name;
            var types = new List<string>
            {
                typeName
            };
            // TODO specify the method we will be using in ServicePulse?
            return hubContext.Clients.All.SendAsync("PushEnvelope", new Envelope { Types = types, Message = @event });
        }
    }
}