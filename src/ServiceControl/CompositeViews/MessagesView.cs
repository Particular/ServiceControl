namespace ServiceControl.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using Infrastructure.RavenDB.Indexes;

    public class MessagesView : CommonResult
    {
        public string MessageId { get; set; }

        public DateTime ProcessedAt { get; set; }
        public string ConversationId { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string[] Query { get; set; }

        public MessageStatus Status { get; set; }
    }
}