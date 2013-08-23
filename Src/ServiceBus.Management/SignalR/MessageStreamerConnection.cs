namespace ServiceBus.Management.SignalR
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using NServiceBus;

    public class MessageStreamerConnection : PersistentConnection
    {
        public IBus Bus { get; set; }

        public MessageStreamerConnection()
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
            return Connection.Broadcast(string.Format("Hello John!, we have a Bus={0}", Bus != null));
        }

        // ReSharper disable once NotAccessedField.Local
        Timer timer;
    }
}