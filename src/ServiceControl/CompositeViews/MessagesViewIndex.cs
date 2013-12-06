namespace ServiceControl.CompositeViews
{
    using System;
    using System.Linq;
    using MessageAuditing;
    using MessageFailures;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class MessagesViewIndex : AbstractMultiMapIndexCreationTask<MessagesView>
    {
        public MessagesViewIndex()
        {
            AddMap<AuditMessage>(messages => messages.Select(message => new
            {
                MessageId = message.MessageId,
                message.Status,
                ProcessedAt = new DateTime(2013, 12, 6),
                ReceivingEndpointName = message.ReceivingEndpoint.Name
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                MessageId = message.MessageId,
                message.Status,
                ProcessedAt = new DateTime(2013, 12, 7),
                ReceivingEndpointName = message.ProcessingAttempts.Last().Message.ProcessingEndpoint.Name
            }));

            Reduce = results => from message in results
                group message by message.MessageId
                into g
                select new MessagesView
                {
                    MessageId = g.Key,
                    Status = g.OrderByDescending(m => m.ProcessedAt).First().Status,
                    ProcessedAt = g.OrderByDescending(m => m.ProcessedAt).First().ProcessedAt,
                    ReceivingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().ReceivingEndpointName,
                };



            ////Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpointName, FieldIndexing.Default);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            //Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}