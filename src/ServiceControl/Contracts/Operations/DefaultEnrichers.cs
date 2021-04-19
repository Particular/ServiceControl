namespace ServiceControl.Contracts.Operations
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Operations;

    public class DefaultEnrichers : Feature
    {
        public DefaultEnrichers() => EnableByDefault();

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<EnrichWithTrackingIds>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MessageTypeEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ProcessingStatisticsEnricher>(DependencyLifecycle.SingleInstance);
        }

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

        class EnrichWithTrackingIds : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
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

        class ProcessingStatisticsEnricher : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
            {
                if (context.Headers.TryGetValue(Headers.TimeSent, out var timeSentValue))
                {
                    var timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                    context.Metadata.Add("TimeSent", timeSent);
                }
            }
        }
    }
}