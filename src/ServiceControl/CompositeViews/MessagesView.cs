namespace ServiceControl.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using Contracts.Operations;
    using Infrastructure.RavenDB.Indexes;
    using NServiceBus;

    public class MessagesView : CommonResult
    {
        public DateTime ProcessedAt { get; set; }
        public string ConversationId { get; set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; set; }

        public string[] Query { get; set; }

        public MessageStatus Status { get; set; }
        public string SendingEndpointName { get; set; }
        public MessageIntentEnum MessageIntent { get; set; }
    }
}