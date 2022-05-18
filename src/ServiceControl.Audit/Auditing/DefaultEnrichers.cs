namespace ServiceControl.Audit.Auditing
{
    using System.Linq;
    using NServiceBus;

    class MessageTypeEnricher : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context)
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

    class EnrichWithTrackingIds : IEnrichImportedAuditMessages
    {
        public void Enrich(AuditEnricherContext context)
        {
            if (context.Headers.TryGetValue(Headers.ConversationId, out var conversationId))
            {
                context.Metadata.Add("ConversationId", conversationId);
            }

            if (context.Headers.TryGetValue(Headers.RelatedTo, out var relatedToId))
            {
                context.Metadata.Add("RelatedToId", relatedToId);
            }
        }
    }
}