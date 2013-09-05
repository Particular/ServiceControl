namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using MessageAuditing;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class Conversations_Sorted : AbstractIndexCreationTask<Message, Messages_Sort.Result>
    {
        public Conversations_Sorted()
        {
            Map = messages => from message in messages
                select new
                {
                    message.Id,
                    message.MessageType,
                    message.TimeSent,
                    message.Status,
                    message.ConversationId,
                    TimeOfFailure =
                        message.FailureDetails != null ? message.FailureDetails.TimeOfFailure : DateTime.MinValue,
                    CriticalTime = message.Statistics != null ? message.Statistics.CriticalTime : TimeSpan.Zero,
                    ProcessingTime = message.Statistics != null ? message.Statistics.ProcessingTime : TimeSpan.Zero,
                };

            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);
        }

        public class Result : CommonResult
        {
            public string ConversationId { get; set; }
        }
    }
}