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
            var isSystemMessage = false;
            if (message.PhysicalMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                isSystemMessage = true;
                message.Metadata.Add("MessageType", "SystemMessage");
            }

            string enclosedMessageTypes;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out enclosedMessageTypes))
            {
                var messageType = GetMessageType(enclosedMessageTypes);
                isSystemMessage = DetectSystemMessage(messageType);
                message.Metadata.Add("MessageType", messageType);
                message.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
            }
            message.Metadata.Add("IsSystemMessage", isSystemMessage);
               
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