namespace ServiceBus.Management.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class Messages_Sort : AbstractIndexCreationTask<Message, Messages_Sort.Result>
    {
        public class Result : CommonResult
        {
            public MessageStatus Status { get; set; }
            public bool IsSystemMessage { get; set; }
        }

        public Messages_Sort()
        {
            Map = messages => from message in messages
                              select new
                                  {
                                      message.Id,
                                      message.MessageType,
                                      message.TimeSent,
                                      message.Status,
                                      message.IsSystemMessage,
                                      ReceivingEndpointName = message.ReceivingEndpoint.Name,
                                      TimeOfFailure = message.FailureDetails != null ? message.FailureDetails.TimeOfFailure : DateTime.MinValue,
                                      CriticalTime = message.Statistics != null ? message.Statistics.CriticalTime : TimeSpan.Zero,
                                      ProcessingTime = message.Statistics != null ? message.Statistics.ProcessingTime : TimeSpan.Zero,
                                  };

            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);
        }
    }
}