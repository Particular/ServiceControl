namespace ServiceBus.Management.SignalR
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;

    public class EndpointsConnection : PersistentConnection
    {
        Timer timer;

        public EndpointsConnection()
        {
            timer = new Timer(Callback, null, 5000, 5000);
        }

        void Callback(object state)
        {
            if (Connection == null)
            {
                return;
            }

            Connection.Broadcast("Are you still there?");
        }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            return Connection.Broadcast("Hello John!");
        }
    }
}