namespace ServiceControl.CompositeViews.Messages
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
                Id = message.UniqueMessageId,
                MessageId = message.MessageMetadata["MessageId"],
                MessageType = message.MessageMetadata["MessageType"],
                MessageIntent = message.MessageMetadata["MessageIntent"],
                IsSystemMessage = message.MessageMetadata["IsSystemMessage"],
                Status = MessageStatus.Successful,
                message.ProcessedAt,
                SendingEndpoint = message.MessageMetadata["SendingEndpoint"],
                ReceivingEndpoint = message.MessageMetadata["ReceivingEndpoint"],
                ReceivingEndpointName = ((EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]).Name,
                ConversationId = message.MessageMetadata["ConversationId"],
                TimeSent = message.MessageMetadata["TimeSent"],
                ProcessingTime = message.MessageMetadata["ProcessingTime"],
                CriticalTime = message.MessageMetadata["CriticalTime"],
                message.Headers,
                BodyUrl = message.MessageMetadata["BodyUrl"],
                BodySize = message.MessageMetadata["BodySize"],
                Query = message.MessageMetadata.Select(kvp => kvp.Value.ToString()).ToArray()
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                message.Id,
                MessageId = message.MostRecentAttempt.MessageMetadata["MessageId"],
                MessageType = message.MostRecentAttempt.MessageMetadata["MessageType"],
                MessageIntent = message.MostRecentAttempt.MessageMetadata["MessageIntent"],
                IsSystemMessage = message.MostRecentAttempt.MessageMetadata["IsSystemMessage"],
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                ProcessedAt = message.MostRecentAttempt.FailureDetails.TimeOfFailure,
                SendingEndpoint = message.MostRecentAttempt.MessageMetadata["SendingEndpoint"],
                ReceivingEndpoint = message.MostRecentAttempt.MessageMetadata["ReceivingEndpoint"],
                ReceivingEndpointName = ((EndpointDetails)message.MostRecentAttempt.MessageMetadata["ReceivingEndpoint"]).Name,
                ConversationId = message.MostRecentAttempt.MessageMetadata["ConversationId"],
                TimeSent = message.MostRecentAttempt.MessageMetadata["TimeSent"],
                ProcessingTime = (object) TimeSpan.Zero,
                CriticalTime = (object) TimeSpan.Zero,
                message.MostRecentAttempt.Headers,
                BodyUrl = message.MostRecentAttempt.MessageMetadata["BodyUrl"],
                BodySize = message.MostRecentAttempt.MessageMetadata["BodySize"],
                Query = message.MostRecentAttempt.MessageMetadata.Select(kvp => kvp.Value.ToString()).ToArray()
            }));

            Reduce = results => from message in results
                group message by message.Id
                into g
                let d = g.OrderByDescending(m => m.ProcessedAt).FirstOrDefault()
                select new MessagesView
                {
                    Id = g.Key,
                    MessageId = d.MessageId,
                    MessageType = d.MessageType,
                    MessageIntent = d.MessageIntent,
                    IsSystemMessage = d.IsSystemMessage,
                    Status = d.Status,
                    ProcessedAt = d.ProcessedAt,
                    SendingEndpoint = d.SendingEndpoint,
                    ReceivingEndpoint = d.ReceivingEndpoint,
                    ReceivingEndpointName = d.ReceivingEndpointName,
                    ConversationId = d.ConversationId,
                    TimeSent = d.TimeSent,
                    ProcessingTime = d.ProcessingTime,
                    CriticalTime = d.CriticalTime,
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seem to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    Headers = d.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                    BodyUrl = d.BodyUrl,
                    BodySize = d.BodySize,
                    Query = d.Query,
                };

            Index(x => x.Query, FieldIndexing.Analyzed);
            Index(x => x.CriticalTime, FieldIndexing.Default);
            Index(x => x.ProcessingTime, FieldIndexing.Default);
            Index(x => x.ProcessedAt, FieldIndexing.Default);

            Sort(x => x.CriticalTime, SortOptions.Long);
            Sort(x => x.ProcessingTime, SortOptions.Long);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}