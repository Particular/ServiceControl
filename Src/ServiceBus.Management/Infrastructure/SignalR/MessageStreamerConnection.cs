namespace ServiceBus.Management.Infrastructure.SignalR
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.Transports;

    public class MessageStreamerConnection : PersistentConnection
    {
        private static readonly Task EmptyTask = MakeEmptyTask();

        static Task MakeEmptyTask()
        {
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return tcs.Task;
        }

        readonly ISendMessages sender;

        public MessageStreamerConnection(ISendMessages sender)
        {
            this.sender = sender;
        }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            var jObject = JObject.Parse(data);

            var jsonMessage = jObject["message"].ToString();
            var headers = jObject["headers"].ToObject<Dictionary<string, string>>();

            var message = new TransportMessage();
            foreach (var header in headers)
            {
                message.Headers[header.Key] = header.Value;
            }
            message.Body = Encoding.UTF8.GetBytes(jsonMessage);

            sender.Send(message, Address.Local);

            return EmptyTask;
        }
    }
}