namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure;
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
            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var isSystemMessage = false;
                string messageType = null;

                if (headers.ContainsKey(Headers.ControlMessageHeader))
                {
                    isSystemMessage = true;
                }

                string enclosedMessageTypes;
                if (headers.TryGetValue(Headers.EnclosedMessageTypes, out enclosedMessageTypes))
                {
                    messageType = GetMessageType(enclosedMessageTypes);
                    isSystemMessage = DetectSystemMessage(messageType);
                    metadata.Add("SearchableMessageType", messageType.Replace(".", " ").Replace("+", " "));
                }

                metadata.Add("IsSystemMessage", isSystemMessage);
                metadata.Add("MessageType", messageType);

                return TaskEx.CompletedTask;
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
            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                string conversationId;

                if (headers.TryGetValue(Headers.ConversationId, out conversationId))
                {
                    metadata.Add("ConversationId", conversationId);
                }

                string relatedToId;

                if (headers.TryGetValue(Headers.RelatedTo, out relatedToId))
                {
                    metadata.Add("RelatedToId", relatedToId);
                }

                return TaskEx.CompletedTask;
            }
        }
    }
}