namespace ServiceControl.CompositeViews
{
    using System;
    using System.Linq;
    using Contracts.Operations;
    using Lucene.Net.Analysis.Standard;
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
                MessageType = message.MessageType,
                Status = MessageStatus.Successful,
                ProcessedAt = new DateTime(2013, 12, 6),
                ReceivingEndpointName = message.ReceivingEndpoint.Name,
                ConversationId = message.ConversationId,
                TimeSent = message.TimeSent
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                MessageId = message.MessageId,
                MessageType = message.ProcessingAttempts.Last().Message.MessageType,
                message.Status,
                ProcessedAt = new DateTime(2013, 12, 7),
                ReceivingEndpointName = message.ProcessingAttempts.Last().Message.ProcessingEndpoint.Name,
                ConversationId = message.ProcessingAttempts.Last().Message.ConversationId,
                TimeSent = message.ProcessingAttempts.Last().Message.TimeSent
            }));

            Reduce = results => from message in results
                group message by message.MessageId
                into g
                select new MessagesView
                {
                    MessageId = g.Key,
                    MessageType = g.OrderByDescending(m => m.ProcessedAt).First().MessageType,
                    Status = g.OrderByDescending(m => m.ProcessedAt).First().Status,
                    ProcessedAt = g.OrderByDescending(m => m.ProcessedAt).First().ProcessedAt,
                    ReceivingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().ReceivingEndpointName,
                    ConversationId = g.OrderByDescending(m => m.ProcessedAt).First().ConversationId,
                    TimeSent = g.OrderByDescending(m => m.ProcessedAt).First().TimeSent,
                };



            //Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpointName, FieldIndexing.Default);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            //Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}