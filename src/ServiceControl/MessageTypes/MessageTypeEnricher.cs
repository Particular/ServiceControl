namespace ServiceControl.MessageTypes
{
    using System.Linq;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.Operations;

    public class MessageTypeEnricher:ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            var isSystemMessage = false;
            string messageType = null;
          
            if (message.PhysicalMessage.Headers.ContainsKey(Headers.ControlMessageHeader))
            {
                isSystemMessage = true;
            }

            string enclosedMessageTypes;
            if (message.PhysicalMessage.Headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes))
            {
                messageType = GetMessageType(enclosedMessageTypes);
                isSystemMessage = DetectSystemMessage(messageType);
                message.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
            }

            message.Metadata.Add("IsSystemMessage", isSystemMessage);
            message.Metadata.Add("MessageType", messageType);   
        }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains("NServiceBus.Scheduling.Messages.ScheduledTask");
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