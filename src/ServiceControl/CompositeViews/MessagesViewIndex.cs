namespace ServiceControl.CompositeViews
{
    using System;
    using System.Collections.Generic;
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
                Id = message.Id,
                MessageId = message.MessageId,
                MessageType = message.MessageType,
                Status = MessageStatus.Successful,
                ProcessedAt = message.ProcessedAt,
                ReceivingEndpointName = message.ReceivingEndpoint.Name,
                ConversationId = message.ConversationId,
                TimeSent = message.TimeSent,
                Headers = message.Headers,
                Query =  new object[]
                    {
                        message.MessageType,
                        message.Body,
                        message.ReceivingEndpoint.Name,
                        message.ReceivingEndpoint.Machine,
                        message.Headers.Select(kvp => string.Format("{0} {1}", kvp.Key, kvp.Value))
                    }
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                Id = message.Id,
                MessageId = message.MessageId,
                MessageType = message.ProcessingAttempts.Last().Message.MessageType,
                message.Status,
                ProcessedAt = message.ProcessingAttempts.Last().FailureDetails.TimeOfFailure,
                ReceivingEndpointName = message.ProcessingAttempts.Last().Message.ReceivingEndpoint.Name,
                ConversationId = message.ProcessingAttempts.Last().Message.ConversationId,
                TimeSent = message.ProcessingAttempts.Last().Message.TimeSent,
                Headers = message.ProcessingAttempts.Last().Message.Headers,
                Query = new object[]
                    {
                        message.ProcessingAttempts.Last().Message.MessageType,
                        message.ProcessingAttempts.Last().Message.ReceivingEndpoint.Name,
                        message.ProcessingAttempts.Last().Message.ReceivingEndpoint.Machine,
                        message.ProcessingAttempts.Last().Message.Headers.Select(kvp => string.Format("{0} {1}", kvp.Key, kvp.Value))
                    }
            }));

            Reduce = results => from message in results
                                group message by message.Id
                                    into g
                                    select new MessagesView
                                    {
                                        Id = g.Key,
                                        MessageId = g.OrderByDescending(m => m.ProcessedAt).First().MessageId,
                                        MessageType = g.OrderByDescending(m => m.ProcessedAt).First().MessageType,
                                        Status = g.OrderByDescending(m => m.ProcessedAt).First().Status,
                                        ProcessedAt = g.OrderByDescending(m => m.ProcessedAt).First().ProcessedAt,
                                        ReceivingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().ReceivingEndpointName,
                                        ConversationId = g.OrderByDescending(m => m.ProcessedAt).First().ConversationId,
                                        TimeSent = g.OrderByDescending(m => m.ProcessedAt).First().TimeSent,
                                        Headers = g.OrderByDescending(m => m.ProcessedAt).First().Headers.Select(header => new KeyValuePair<string, string>(header.Key, header.Value)),
                                        Query = g.OrderByDescending(m => m.ProcessedAt).First().Query
                                    };


            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.ReceivingEndpointName, FieldIndexing.Default);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}