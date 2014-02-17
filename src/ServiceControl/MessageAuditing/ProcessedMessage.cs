namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using Contracts.Operations;

    public class ProcessedMessage
    {
        const int MaxBodySizeToStore = 1024 * 100; //100 kb

        public ProcessedMessage()
        {
           MessageMetadata = new Dictionary<string, object>();
        }

        public ProcessedMessage(ImportSuccessfullyProcessedMessage message)
        {
            Id = "ProcessedMessages/" + message.UniqueMessageId;
            UniqueMessageId = message.UniqueMessageId;            
            MessageMetadata = message.Metadata;
            Headers = message.PhysicalMessage.Headers;

            AddBody(message);

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

        void AddBody(ImportMessage message)
        {
            if (message.PhysicalMessage.Body == null || message.PhysicalMessage.Body.Length == 0 || message.PhysicalMessage.Body.Length > MaxBodySizeToStore)
            {
                return;
            }

            string contentType;

            if (!message.PhysicalMessage.Headers.TryGetValue(NServiceBus.Headers.ContentType, out contentType))
            {
                contentType = "application/xml"; //default to xml for now
            }

            message.Metadata.Add("ContentType", contentType);

            if (contentType.ToLower().Contains("binary"))
            {
                return;
            }

            Body = message.PhysicalMessage.Body;
        }
        
        public string Id { get; set; }

        public string UniqueMessageId { get; set; }

        public byte[] Body { get; set; }

        public Dictionary<string, object> MessageMetadata { get; set; }

        public Dictionary<string, string> Headers { get; set; }
         
        public DateTime ProcessedAt { get; set; }
    }
}