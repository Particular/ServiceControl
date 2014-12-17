namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Contracts.Operations;

    public class AuditProcessedMessage
    {
        public AuditProcessedMessage()
        {
           MessageMetadata = new Dictionary<string, object>();
        }

        public AuditProcessedMessage(ImportSuccessfullyProcessedMessage message)
        {
            UniqueMessageId = message.UniqueMessageId;            
            MessageMetadata = message.Metadata;
            Headers = message.PhysicalMessage.Headers;
            Status = MessageStatus.Successful;

            object retried;
            if (message.Metadata.TryGetValue("IsRetried", out retried) && (bool) retried)
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
                ProcessedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                ProcessedAt = DateTime.UtcNow;//best guess    
            }
        }

        public string Id { get; set; }
        public MessageStatus Status { get; set; }
        public string UniqueMessageId { get; set; }

        public Dictionary<string, object> MessageMetadata { get; set; }

        public Dictionary<string, string> Headers { get; set; }
         
        public DateTime ProcessedAt { get; set; }
    }
}