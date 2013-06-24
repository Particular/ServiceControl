namespace ServiceBus.Management.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Lucene.Net.Analysis.Standard;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class Messages_Search : AbstractIndexCreationTask<Message, Messages_Search.Result>
    {
        public class Result
        {
            public string Query { get; set; }
            public string ReceivingEndpoint { get; set; }
            public string Id { get; set; }
            public string MessageType { get; set; }
            public DateTime TimeSent { get; set; }
            public DateTime TimeOfFailure { get; set; }
            public TimeSpan CriticalTime { get; set; }
            public TimeSpan ProcessingTime { get; set; }
        }

        public Messages_Search()
        {
            Map = messages => from message in messages
                            select new
                                {
                                    message.Id, 
                                    message.MessageType, 
                                    message.TimeSent,
                                    TimeOfFailure = message.FailureDetails != null ? message.FailureDetails.TimeOfFailure : DateTime.MinValue,
                                    CriticalTime = message.Statistics != null ? message.Statistics.CriticalTime : TimeSpan.Zero,
                                    ProcessingTime = message.Statistics != null ? message.Statistics.ProcessingTime : TimeSpan.Zero,
                                    Query = new object[]
                                        {
                                            message.MessageType,
                                            message.Body,
                                            message.ReceivingEndpoint.Name,
                                            message.ReceivingEndpoint.Machine,
                                            message.Headers.Select(kvp => String.Format("{0} {1}", kvp.Key, kvp.Value)),
                                        },
                                    ReceivingEndpoint = message.ReceivingEndpoint.Name
                                };

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpoint, FieldIndexing.Default);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}