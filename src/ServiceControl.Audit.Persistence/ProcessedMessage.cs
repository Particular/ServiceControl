namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;
    using ServiceControl.Audit.Persistence.Infrastructure;

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

            var processingStartedTicks =
                headers.TryGetValue(NServiceBus.Headers.ProcessingStarted, out var processingStartedValue)
                    ? DateTimeOffsetHelper.ToDateTimeOffset(processingStartedValue).UtcDateTime.Ticks.ToString()
#pragma warning disable RS0030 // Do not use banned apis: Missing message timestamps fall back to the current wall-clock time
                    : DateTime.UtcNow.Ticks.ToString();
#pragma warning restore RS0030

            var documentId = $"{processingStartedTicks}-{headers.ProcessingId()}";

            Id = $"ProcessedMessages-{documentId}";

            ProcessedAt = Headers.TryGetValue(NServiceBus.Headers.ProcessingEnded, out var processedAt) ?
#pragma warning disable RS0030 // Do not use banned apis: Missing message timestamps fall back to the current wall-clock time
                DateTimeOffsetHelper.ToDateTimeOffset(processedAt).UtcDateTime : DateTime.UtcNow; // best guess
#pragma warning restore RS0030
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