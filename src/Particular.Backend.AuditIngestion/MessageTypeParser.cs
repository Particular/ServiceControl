namespace ServiceControl.MessageTypes
{
    using System.Linq;
    using Particular.Operations.Ingestion.Api;

    public class MessageTypeParser
    {
        public  MessageType Parse(HeaderCollection headers)
        {
            var isControlMessage = headers.Has("NServiceBus.ControlMessage");
            if (isControlMessage)
            {
                return MessageType.Control;
            }
            string enclosedMessageTypes;
            if (headers.TryGet("NServiceBus.EnclosedMessageTypes", out enclosedMessageTypes))
            {
                var messageType = GetMessageType(enclosedMessageTypes);
                var isSystemMessage = DetectSystemMessage(messageType);
                //message.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
                return new MessageType(messageType, isSystemMessage);
            }
            return MessageType.Unknown;
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