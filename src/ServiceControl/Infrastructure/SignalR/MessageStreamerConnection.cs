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
    using NServiceBus.Transport;

    public class MessageStreamerConnection : PersistentConnection
    {
        public MessageStreamerConnection(IDispatchMessages sender, ReadOnlySettings settings)
        {
            var conventions = settings.Get<Conventions>();
            this.sender = sender;

            messageTypes = settings.GetAvailableTypes()
                                        .Where(conventions.IsMessageType)
                                        .GroupBy(x => x.Name)
                                        .ToDictionary(x => x.Key, x => x.FirstOrDefault().AssemblyQualifiedName);
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
                    {Headers.EnclosedMessageTypes, messageTypes[messageType]}
                }, Encoding.UTF8.GetBytes(jsonMessage));
                var transportOperation = new TransportOperation(message, new UnicastAddressTag(localAddress));
                var transportOperations = new TransportOperations(transportOperation);

                await sender.Dispatch(transportOperations, transportTransaction, contextBag).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to process SignalR message. AuditMessage={data}", ex);
                throw;
            }
        }

        static readonly ILog Log = LogManager.GetLogger(typeof(MessageStreamerConnection));
        readonly Dictionary<string, string> messageTypes;
        readonly IDispatchMessages sender;
        string localAddress;
        static TransportTransaction transportTransaction = new TransportTransaction();
        static ContextBag contextBag = new ContextBag();
    }
}