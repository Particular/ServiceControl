namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Contracts.Operations;

    public class ProcessedMessage
    {
        public ProcessedMessage(ImportSuccessfullyProcessedMessage message)
        {

            Id = "ProcessedMessages/" + message.UniqueMessageId;
            UniqueMessageId = message.UniqueMessageId;            
            MessageMetadata = message.Metadata;
            Headers = message.PhysicalMessage.Headers;

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

        public string UniqueMessageId { get; set; }

        public Dictionary<string, MessageMetadata> MessageMetadata { get; set; }

        public Dictionary<string, string> Headers { get; set; }
         
        public DateTime ProcessedAt { get; set; }
    }
}