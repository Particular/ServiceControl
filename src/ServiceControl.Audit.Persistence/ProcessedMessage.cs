﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class ProcessedMessage
    {
        public ProcessedMessage()
        {
            MessageMetadata = new Dictionary<string, object>();
            Headers = new Dictionary<string, string>();
        }

        public ProcessedMessage(string uniqueMessageId, Dictionary<string, string> headers, Dictionary<string, object> metadata)
        {
            UniqueMessageId = uniqueMessageId;
            MessageMetadata = metadata;
            Headers = headers;

            if (Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out var processedAt))
            {
                ProcessedAt = DateTimeExtensions.ToUtcDateTime(processedAt);
            }
            else
            {
                ProcessedAt = DateTime.UtcNow; // best guess
            }
        }

        public string Id { get; set; }

        public string UniqueMessageId { get; set; }

        public Dictionary<string, object> MessageMetadata { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public DateTime ProcessedAt { get; set; }

        // non-indexed body when the body is stored on the document
        public string Body { get; set; }
    }
}