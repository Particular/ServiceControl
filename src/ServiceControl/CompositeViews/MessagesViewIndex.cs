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
            AddMap<ProcessedMessage>(messages => messages.Select(message => new
            {
                Id = message.Id,
                MessageId = message.MessageMetadata["MessageId"].Value,
                MessageType = message.MessageMetadata["MessageType"].Value,
                Status = MessageStatus.Successful,
                ProcessedAt = message.ProcessedAt,
                ReceivingEndpointName = message.ReceivingEndpoint.Name,
                ConversationId = message.MessageMetadata["ConversationId"].Value,
                TimeSent = message.MessageMetadata["TimeSent"].Value,
                Headers = message.Headers,
                Query = message.MessageMetadata.Values.SelectMany(m => m.SearchTokens).ToArray()
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                Id = message.Id,
                MessageId = message.MostRecentAttempt.MessageMetadata["MessageId"].Value,
                MessageType = message.ProcessingAttempts.Last().MessageMetadata["MessageType"].Value,
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                ProcessedAt = message.ProcessingAttempts.Last().FailureDetails.TimeOfFailure,
                ReceivingEndpointName = message.ProcessingAttempts.Last().FailingEndpoint.Name,
                ConversationId = message.ProcessingAttempts.Last().MessageMetadata["ConversationId"].Value,
                TimeSent = message.ProcessingAttempts.Last().MessageMetadata["TimeSent"].Value,
                Headers = message.ProcessingAttempts.Last().Headers,
                Query = message.MostRecentAttempt.MessageMetadata.Values.SelectMany(m=>m.SearchTokens).ToArray()
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