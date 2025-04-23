﻿namespace ServiceControl.MessageAuditing
{
    using System;
    using System.Collections.Generic;

    public class ProcessedMessage
    {
        public string Id { get; set; }

        public string UniqueMessageId { get; set; }

        public Dictionary<string, object> MessageMetadata { get; set; } = [];

        public Dictionary<string, string> Headers { get; set; } = [];

        public DateTime ProcessedAt { get; set; }
    }
}