namespace ServiceControl.Operations.Audit
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NServiceBus;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations.BodyStorage;

    class ProcessedMessageFactory
    {
        private const string SEPARATOR = " ";
        private IMessageBodyStoragePolicy auditBodyStoragePolicy;
        private readonly IMessageBodyStore messageBodyStore;
        private IEnrichImportedMessages[] enrichers;

        public ProcessedMessageFactory(IEnrichImportedMessages[] enrichers, IMessageBodyStoragePolicy auditBodyStoragePolicy, IMessageBodyStore messageBodyStore)
        {
            this.enrichers = enrichers;
            this.auditBodyStoragePolicy = auditBodyStoragePolicy;
            this.messageBodyStore = messageBodyStore;
        }

        public ProcessedMessage Create(Dictionary<string, string> headers)
        {
            var metadata = new Dictionary<string, object>();

            // TODO: Move these to an enricher once error ingestion stops relying on ImportMessage
            DictionaryExtensions.CheckIfKeyExists(Headers.MessageId, headers, messageId => metadata.Add("MessageId", messageId));

            // NOTE: Pulled out of the TransportMessage class
            var intent = (MessageIntentEnum)0;
            string str;
            if (headers.TryGetValue("NServiceBus.MessageIntent", out str))
            {
                Enum.TryParse(str, true, out intent);
            }

            metadata.Add("MessageIntent", intent);
            metadata.Add("HeadersForSearching", string.Join(SEPARATOR, headers.Values));

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(headers, metadata);
            }

            return new ProcessedMessage
            {
                Id = $"ProcessedMessages/{Guid.NewGuid()}",
                Headers = headers,
                MessageMetadata = metadata,
                ProcessedAt = GetProcessedAt(headers),
                UniqueMessageId = headers.UniqueMessageId()
            };
        }

        private DateTime GetProcessedAt(IReadOnlyDictionary<string, string> headers)
        {
            string processedAt;

            return headers.TryGetValue(Headers.ProcessingEnded, out processedAt) ? DateTimeExtensions.ToUtcDateTime(processedAt) : DateTime.UtcNow;
        }

        public void AddBodyDetails(ProcessedMessage processedMessage, ClaimsCheck bodyStorageClaimsCheck)
        {
            WriteMetadata(processedMessage.MessageMetadata, bodyStorageClaimsCheck.Metadata);

            if (!bodyStorageClaimsCheck.Stored)
            {
                processedMessage.MessageMetadata.Add("BodyNotStored", true);
            }
            else if (auditBodyStoragePolicy.ShouldIndex(bodyStorageClaimsCheck.Metadata))
            {
                byte[] messageBody;
                MessageBodyMetadata metadata;

                if (messageBodyStore.TryGet(bodyStorageClaimsCheck.Metadata.MessageId, out messageBody, out metadata))
                {
                    processedMessage.MessageMetadata.Add("Body", Encoding.UTF8.GetString(messageBody));
                }
            }
        }

        private static void WriteMetadata(IDictionary<string, object> messageMeta, MessageBodyMetadata bodyMeta)
        {
            messageMeta.Add("ContentLength", bodyMeta.Size);
            messageMeta.Add("ContentType", bodyMeta.ContentType);
            messageMeta.Add("BodyUrl", $"/messages/{bodyMeta.MessageId}/body_v2");
        }
    }
}