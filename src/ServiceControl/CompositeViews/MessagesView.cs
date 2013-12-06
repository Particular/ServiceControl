namespace ServiceControl.CompositeViews
{
    using System;
    using Infrastructure.RavenDB.Indexes;

    public class MessagesView : CommonResult
    {
        public string MessageId { get; set; }

        public DateTime ProcessedAt { get; set; }
    }
}