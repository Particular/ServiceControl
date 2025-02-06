namespace ServiceControl.Infrastructure.SignalR
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;

    class GlobalEventHandler(IHubContext<MessageStreamerHub> hubContext)
    {
        public Task Broadcast(IUserInterfaceEvent @event, CancellationToken cancellationToken)
        {
            var typeName = @event.GetType().Name;
            return hubContext.Clients.All.SendAsync("PushEnvelope", new Envelope { Types = [typeName], Message = @event }, cancellationToken);
        }
    }
}