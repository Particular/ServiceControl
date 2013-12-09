namespace ServiceControl.CompositeViews
{
    using System;
    using Infrastructure.RavenDB.Indexes;

    public class MessagesView : CommonResult
    {
        public string MessageId { get; set; }

        public DateTime ProcessedAt { get; set; }
        public string ConversationId { get; set; }

        //public string Query { get; set; }
    }
}