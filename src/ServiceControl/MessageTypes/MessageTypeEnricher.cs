namespace ServiceControl.MessageTypes
{
    using System.Linq;
    using Contracts.Operations;
    using Operations;
    using NServiceBus.Scheduling.Messages;

    public class MessageTypeEnricher:ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            if (message.PhysicalMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                message.Add(new MessageMetadata("IsSystemMessage", "true"));
                message.Add(new MessageMetadata("MessageType", "SystemMessage"));
            }

            string enclosedMessageTypes;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out enclosedMessageTypes))
            {
                var messageType = GetMessageType(enclosedMessageTypes);
                message.Add(new MessageMetadata("IsSystemMessage", DetectSystemMessage(messageType).ToString()));
                message.Add(new MessageMetadata("MessageType", messageType));
                message.Add(new MessageMetadata("SearchableMessageType", messageType.Replace("."," ").Replace("+"," ")));
            }
        }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains(typeof(ScheduledTask).FullName);
        }

        string GetMessageType(string messageTypeString)
        {
            if (!messageTypeString.Contains(","))
            {
                return messageTypeString;
            }

            return messageTypeString.Split(',').First();
        }
    }
}