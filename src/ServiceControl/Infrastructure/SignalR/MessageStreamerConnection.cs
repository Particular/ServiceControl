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
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transports;

    public class MessageStreamerConnection : PersistentConnection
    {
        public MessageStreamerConnection(IDispatchMessages dispatcher, ReadOnlySettings settings, Conventions conventions)
        {
            this.dispatcher = dispatcher;

            messageTypes = settings.GetAvailableTypes()
                                        .Where(conventions.IsMessageType)
                                        .ToList();
            localAddress = settings.LocalAddress();
        }

        protected override async Task OnReceived(IRequest request, string connectionId, string data)
        {
            try
            {
                var jObject = JObject.Parse(data);

                var jsonMessage = jObject["message"].ToString();
                var messageType = jObject["type"].ToString();
                
                var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>
                {
                    { Headers.EnclosedMessageTypes, MapMessageType(messageType) },
                    { Headers.ContentType, ContentTypes.Json }
                }, Encoding.UTF8.GetBytes(jsonMessage));

                var operation = new TransportOperation(message, new UnicastAddressTag(localAddress)); // TODO: Do we require specific consinstency from the transport?
                await dispatcher.Dispatch(new TransportOperations(operation), new ContextBag()).ConfigureAwait(false);
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

        static readonly ILog Log = LogManager.GetLogger(typeof(MessageStreamerConnection));
        readonly List<Type> messageTypes;
        readonly IDispatchMessages dispatcher;
        string localAddress;
    }
}