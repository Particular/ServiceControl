namespace ServiceControl.Migrations
{
    using System;
    using System.Collections.Generic;

    public class OldProcessedMessage
    {
        public OldProcessedMessage()
        {
            MessageMetadata = new Dictionary<string, object>();
        }

        public string Id { get; set; }
        public string UniqueMessageId { get; set; }
        public Dictionary<string, object> MessageMetadata { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}