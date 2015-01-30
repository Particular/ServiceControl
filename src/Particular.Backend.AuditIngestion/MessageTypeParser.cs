namespace ServiceControl.MessageTypes
{
    using System.Linq;
    using Particular.Backend.AuditIngestion.Api;
    using ServiceControl.Shell.Api.Ingestion;

    public class MessageTypeParser
    {
        public  MessageType Parse(HeaderCollection headers)
        {
            var isSystemMessage = false;
            string messageType = null;

            if (headers.Has("NServiceBus.ControlMessage"))
            {
                isSystemMessage = true;
            }

            string enclosedMessageTypes;
            if (headers.TryGet("NServiceBus.EnclosedMessageTypes", out enclosedMessageTypes))
            {
                messageType = GetMessageType(enclosedMessageTypes);
                isSystemMessage = DetectSystemMessage(messageType);
                message.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
            }
            return new MessageType(messageType, isSystemMessage);
        }

        static bool DetectSystemMessage(string messageTypeString)
        {
            return messageTypeString.Contains("NServiceBus.Scheduling.Messages.ScheduledTask");
        }

        static string GetMessageType(string messageTypeString)
        {
            return !messageTypeString.Contains(",") 
                ? messageTypeString 
                : messageTypeString.Split(',').First();
        }
    }
}