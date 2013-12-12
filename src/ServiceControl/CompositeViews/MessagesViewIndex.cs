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
                MessageType = message.MessageMetadata["MessageType"].Value,
                MessageIntent = message.MessageMetadata["MessageIntent"].Value,
                IsSystemMessage = message.MessageMetadata["IsSystemMessage"].Value,
                Status = MessageStatus.Successful,
                ProcessedAt = message.ProcessedAt,
                SendingEndpointName = message.MessageMetadata["SendingEndpoint"].Value,
                ReceivingEndpointName = message.MessageMetadata["ReceivingEndpoint"].Value,
                ConversationId = message.MessageMetadata["ConversationId"].Value,
                TimeSent = message.MessageMetadata["TimeSent"].Value,
                ProcessingTime = message.MessageMetadata["ProcessingTime"].Value,
                CriticalTime = message.MessageMetadata["CriticalTime"].Value,
                Headers = message.Headers,
                Query = message.MessageMetadata.SelectMany(kvp => kvp.Value.SearchTokens).ToArray()
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                Id = message.Id,
                MessageType = message.MostRecentAttempt.MessageMetadata["MessageType"].Value,
                MessageIntent = message.MostRecentAttempt.MessageMetadata["MessageIntent"].Value,
                IsSystemMessage = message.MostRecentAttempt.MessageMetadata["IsSystemMessage"].Value,
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                ProcessedAt = message.MostRecentAttempt.FailureDetails.TimeOfFailure,
                SendingEndpointName = message.MostRecentAttempt.MessageMetadata["SendingEndpoint"].Value,
                ReceivingEndpointName = message.MostRecentAttempt.MessageMetadata["ReceivingEndpoint"].Value,
                ConversationId = message.MostRecentAttempt.MessageMetadata["ConversationId"].Value,
                TimeSent = message.MostRecentAttempt.MessageMetadata["TimeSent"].Value,
                ProcessingTime = TimeSpan.Zero,
                CriticalTime = TimeSpan.Zero,
                Headers = message.MostRecentAttempt.Headers,
                Query = message.MostRecentAttempt.MessageMetadata.SelectMany(kvp => kvp.Value.SearchTokens).ToArray()
            }));

            Reduce = results => from message in results
                                group message by message.Id
                                    into g
                                    select new MessagesView
                                    {
                                        Id = g.Key,
                                        MessageType = g.OrderByDescending(m => m.ProcessedAt).First().MessageType,
                                        MessageIntent = g.OrderByDescending(m => m.ProcessedAt).First().MessageIntent,
                                        IsSystemMessage = g.OrderByDescending(m => m.ProcessedAt).First().IsSystemMessage,
                                        Status = g.OrderByDescending(m => m.ProcessedAt).First().Status,
                                        ProcessedAt = g.OrderByDescending(m => m.ProcessedAt).First().ProcessedAt,
                                        SendingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().SendingEndpointName,
                                        ReceivingEndpointName = g.OrderByDescending(m => m.ProcessedAt).First().ReceivingEndpointName,
                                        ConversationId = g.OrderByDescending(m => m.ProcessedAt).First().ConversationId,
                                        TimeSent = g.OrderByDescending(m => m.ProcessedAt).First().TimeSent,
                                        ProcessingTime = g.OrderByDescending(m => m.ProcessedAt).First().ProcessingTime,
                                        CriticalTime = g.OrderByDescending(m => m.ProcessedAt).First().CriticalTime,
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