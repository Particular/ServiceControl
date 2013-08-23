namespace ServiceBus.Management.SignalR
{
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;

    public class MessageStreamerConnection : PersistentConnection
    {
        public IBus Bus { get; set; }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            return Connection.Broadcast(string.Format("Hello John!, we have a Bus={0}", Bus != null));
        }
    }
}