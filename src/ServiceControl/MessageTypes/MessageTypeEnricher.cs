namespace ServiceControl.MessageTypes
{
    using System.Linq;
    using Contracts.Operations;
    using Operations;

    public class MessageTypeEnricher:ImportEnricher
    {
        public override void Enrich(ImportMessage message)
        {
            if (message.PhysicalMessage.Headers.ContainsKey(NServiceBus.Headers.ControlMessageHeader))
            {
                message.Metadata.Add("IsSystemMessage", true);
                message.Metadata.Add("MessageType", "SystemMessage");
            }

            string enclosedMessageTypes;

            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.EnclosedMessageTypes, out enclosedMessageTypes))
            {
                var messageType = GetMessageType(enclosedMessageTypes);
                message.Metadata.Add("IsSystemMessage", DetectSystemMessage(messageType));
                message.Metadata.Add("MessageType", messageType);
                message.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
            }
        }

        bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains("NServiceBus.Scheduling.ScheduledTask");
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