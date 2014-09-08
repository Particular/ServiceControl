namespace ServiceControl.Infrastructure.SignalR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNet.SignalR;
    using Newtonsoft.Json.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transports;

    public class MessageStreamerConnection : PersistentConnection
    {
        public MessageStreamerConnection()
        {

            //todo are we using the push from SPA to SC feature John?
            //sender = NServiceBus.Configure.Instance.Builder.Build<ISendMessages>();

            //messageTypes = NServiceBus.Configure.TypesToScan.Where(MessageConventionExtensions.IsMessageType).ToList();
        }

        static Task MakeEmptyTask()
        {
            var completionSource = new TaskCompletionSource<object>();
            completionSource.SetResult(null);
            return completionSource.Task;
        }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            try
            {
                var jObject = JObject.Parse(data);

                var jsonMessage = jObject["message"].ToString();
                var messageType = jObject["type"].ToString();

                var message = new TransportMessage();
                message.Headers[Headers.EnclosedMessageTypes] = MapMessageType(messageType);
                message.Body = Encoding.UTF8.GetBytes(jsonMessage);

                //sender.Send(message, Address.Local);

                return EmptyTask;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Failed to process SignalR message. AuditMessage={0}", data), ex);
                throw;
            }
        }

        string MapMessageType(string className)
        {
            return messageTypes.Single(t => t.Name == className).AssemblyQualifiedName;
        }

        static readonly Task EmptyTask = MakeEmptyTask();
        static readonly ILog Log = LogManager.GetLogger(typeof(MessageStreamerConnection));
        readonly List<Type> messageTypes;
        readonly ISendMessages sender;
    }
}