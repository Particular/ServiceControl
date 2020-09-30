namespace ServiceControl.Audit.Auditing
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;

    public class DefaultEnrichers : Feature
    {
        public DefaultEnrichers()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<EnrichWithTrackingIds>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MessageTypeEnricher>(DependencyLifecycle.SingleInstance);
        }

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
                    context.SearchTerms.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
                }

                context.ProcessedMessage.IsSystemMessage = isSystemMessage;
                context.ProcessedMessage.MessageType = messageType;
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
                    context.ProcessedMessage.ConversationId = conversationId;
                    context.SearchTerms.Add("ConversationId", conversationId);
                }

                if (context.Headers.TryGetValue(Headers.RelatedTo, out var relatedToId))
                {
                    context.SearchTerms.Add("RelatedToId", relatedToId);
                }
            }
        }
    }
}