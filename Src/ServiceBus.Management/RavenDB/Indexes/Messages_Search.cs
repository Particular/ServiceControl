namespace ServiceBus.Management.RavenDB.Indexes
{
    using System;
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class Messages_Search : AbstractIndexCreationTask<Message, Messages_Search.Result>
    {
        public class Result
        {
            public string Query { get; set; }
            public string ReceivingEndpoint { get; set; }
        }

        public Messages_Search()
        {
            Map = messages => from message in messages
                            select new
                                {
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

            Analyze(x => x.Query, "Lucene.Net.Analysis.Standard.StandardAnalyzer");

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpoint, FieldIndexing.Default);
        }
    }
}