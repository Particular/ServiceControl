namespace ServiceControl.Infrastructure.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Lucene.Net.Analysis.Standard;
    using MessageAuditing;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class Messages_Search : AbstractIndexCreationTask<Message, Messages_Search.Result>
    {
        public Messages_Search()
        {
            Map = messages => from message in messages
                select new
                {
                    message.Id,
                    message.MessageType,
                    message.TimeSent,
                    message.Status,
                    TimeOfFailure =
                        message.FailureDetails != null ? message.FailureDetails.TimeOfFailure : DateTime.MinValue,
                    CriticalTime = message.Statistics != null ? message.Statistics.CriticalTime : TimeSpan.Zero,
                    ProcessingTime = message.Statistics != null ? message.Statistics.ProcessingTime : TimeSpan.Zero,
                    Query = new object[]
                    {
                        message.MessageType,
                        message.Body,
                        message.ReceivingEndpoint.Name,
                        message.ReceivingEndpoint.Machine,
                        message.Headers.Select(kvp => String.Format("{0} {1}", kvp.Key, kvp.Value))
                    },
                    ReceivingEndpointName = message.ReceivingEndpoint.Name
                };

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpointName, FieldIndexing.Default);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }

        public class Result : CommonResult
        {
            public string Query { get; set; }
        }
    }
}