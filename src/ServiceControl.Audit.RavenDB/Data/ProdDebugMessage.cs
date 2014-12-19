namespace ServiceControl.ProductionDebugging.RavenDB.Data
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class ProdDebugMessage
    {
        public ProdDebugMessage()
        {
            MessageMetadata = new Dictionary<string, object>();
            Headers = new Dictionary<string, string>();
        }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return "ProdDebuggingMessages/" + messageUniqueId;
        }

        public void Update(ImportSuccessfullyProcessedMessage message)
        {
            Id = MakeDocumentId(message.UniqueMessageId);

            UniqueMessageId = message.UniqueMessageId;
            MessageMetadata = message.Metadata;
            Headers = message.PhysicalMessage.Headers;
            Status = MessageStatus.Successful;

            object retried;
            if (message.Metadata.TryGetValue("IsRetried", out retried) && (bool)retried)
            {
                Status = MessageStatus.ResolvedSuccessfully;
            }
            else
            {
                Status = MessageStatus.Successful;
            }

            string processedAt;
            if (message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out processedAt))
            {
                AttemptedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                AttemptedAt = DateTime.UtcNow;//best guess    
            }
        }

        public void Update(ImportFailedMessage message)
        {
            Id = MakeDocumentId(message.UniqueMessageId);

            //ingore if we have the most recent failure
            if (AttemptedAt >= message.FailureDetails.TimeOfFailure)
            {
                return;
            }

            UniqueMessageId = message.UniqueMessageId;
            Status = AttemptedAt == DateTime.MinValue
                ? MessageStatus.Failed
                : MessageStatus.RepeatedFailure;

            AttemptedAt = message.FailureDetails.TimeOfFailure;
            Headers = message.PhysicalMessage.Headers;
            MessageMetadata = message.Metadata;
        }

        public string Id { get; set; }
        public MessageStatus Status { get; set; }
        public string UniqueMessageId { get; set; }
        public Dictionary<string, object> MessageMetadata { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public DateTime AttemptedAt { get; set; }
    }
}