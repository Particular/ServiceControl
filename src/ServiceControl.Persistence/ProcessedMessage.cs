namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using ServiceControl.Persistence.Infrastructure;

    public class ProcessedMessage
    {
        public ProcessedMessage()
        {
            MessageMetadata = [];
            Headers = [];
        }

        public ProcessedMessage(Dictionary<string, string> headers, Dictionary<string, object> metadata)
        {
            UniqueMessageId = headers.UniqueId();
            MessageMetadata = metadata;
            Headers = headers;

            ProcessedAt = Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out var processedAt) ?
                DateTimeOffsetHelper.ToDateTimeOffset(processedAt).UtcDateTime : DateTime.UtcNow; // best guess
        }

        public string Id { get; set; }

        public string UniqueMessageId { get; set; }

        public Dictionary<string, object> MessageMetadata { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public DateTime ProcessedAt { get; set; }
    }
}