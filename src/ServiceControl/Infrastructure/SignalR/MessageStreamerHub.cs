namespace ServiceControl.Infrastructure.SignalR
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Settings;
    using NServiceBus.Transport;

    class MessageStreamerHub : Hub
    {
        public MessageStreamerHub(
            IMessageDispatcher sender,
            IReadOnlySettings settings,
            ReceiveAddresses receiveAddresses,
            ILogger<MessageStreamerHub> logger)
        {
            var conventions = settings.Get<Conventions>();
            this.sender = sender;
            messageTypes = settings.GetAvailableTypes()
                .Where(conventions.IsMessageType)
                .GroupBy(x => x.Name)
                .ToDictionary(x => x.Key, x => x.FirstOrDefault().AssemblyQualifiedName);
            localAddress = receiveAddresses.MainReceiveAddress;
            this.logger = logger;
        }

        public async Task SendMessage(string data)
        {
            try
            {
                var jsonNode = JsonNode.Parse(data);

                var jsonMessage = jsonNode["message"].ToString();
                var messageType = jsonNode["type"].ToString();

                var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>
                {
                    {Headers.EnclosedMessageTypes, messageTypes[messageType] }
                }, Encoding.UTF8.GetBytes(jsonMessage));
                var transportOperation = new TransportOperation(message, new UnicastAddressTag(localAddress));
                var transportOperations = new TransportOperations(transportOperation);

                await sender.Dispatch(transportOperations, new TransportTransaction());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process SignalR message. AuditMessage={AuditMessage}", data);
                throw;
            }
        }

        readonly Dictionary<string, string> messageTypes;
        readonly IMessageDispatcher sender;
        string localAddress;

        readonly ILogger<MessageStreamerHub> logger;
    }
}