namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.Infrastructure.RavenDB.Indexes;
    using ServiceControl.MessageAuditing;

    public class Messages_Sort : AbstractIndexCreationTask<AuditMessage, Messages_Sort.Result>
    {
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
            public bool IsSystemMessage { get; set; }
        }
    }
}