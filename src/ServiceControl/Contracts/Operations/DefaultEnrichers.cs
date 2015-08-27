namespace ServiceControl.Contracts.Operations
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Operations;

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

        class MessageTypeEnricher : ImportEnricher
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

        class EnrichWithTrackingIds : ImportEnricher
        {
            public override void Enrich(ImportMessage message)
            {
                string conversationId;

                if (message.PhysicalMessage.Headers.TryGetValue(Headers.ConversationId, out conversationId))
                {
                    message.Metadata.Add("ConversationId", conversationId);
                }

                string relatedToId;

                if (message.PhysicalMessage.Headers.TryGetValue(Headers.RelatedTo, out relatedToId))
                {
                    message.Metadata.Add("RelatedToId", relatedToId);
                }
            }
        }
    }
}