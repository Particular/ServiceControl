namespace ServiceControl.Contracts.Operations
{
    using System.Linq;
    using NServiceBus;
    using ServiceControl.Operations;

    class MessageTypeEnricher : IEnrichImportedErrorMessages
    {
        static readonly char[] EnclosedMessageTypeSeparator = { ',' };

        public void Enrich(ErrorEnricherContext context)
        {
            var isSystemMessage = false;
            string messageType = null;

            if (context.Headers.ContainsKey(Headers.ControlMessageHeader))
            {
                isSystemMessage = true;
            }

            if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var enclosedMessageTypes))
            {
                messageType = GetMessageType(enclosedMessageTypes);
                isSystemMessage = DetectSystemMessage(messageType);
                context.Metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
            }

            context.Metadata.Add("IsSystemMessage", isSystemMessage);
            context.Metadata.Add("MessageType", messageType);
        }

        static bool DetectSystemMessage(string messageTypeString) => messageTypeString.Contains("NServiceBus.Scheduling.Messages.ScheduledTask");

        static string GetMessageType(string messageTypeString) => !messageTypeString.Contains(",") ? messageTypeString : messageTypeString.Split(EnclosedMessageTypeSeparator).First();
    }
}