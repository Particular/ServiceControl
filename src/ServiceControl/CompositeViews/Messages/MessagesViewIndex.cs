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

    public class MessagesViewIndex : AbstractMultiMapIndexCreationTask<MessagesViewIndex.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public DateTime ProcessedAt { get; set; }
            public bool IsSystemMessage { get; set; }
        }
        public class MergedMessage
        {
            public string UniqueMessageId { get; set; }

            public Dictionary<string, MessageMetadata> MessageMetadata { get; set; }
            public FailedMessage.ProcessingAttempt MostRecentAttempt { get; set; }
        }

        public MessagesViewIndex()
        {
            AddMap<ProcessedMessage>(messages => messages.Select(message => new
            {
                message.Id,
                MessageId = message.MessageMetadata["MessageId"],
                MessageType = message.MessageMetadata["MessageType"],
                MessageIntent = message.MessageMetadata["MessageIntent"],
                IsSystemMessage = message.MessageMetadata["IsSystemMessage"],
                Status = MessageStatus.Successful,
                message.ProcessedAt,
                IsSystemMessage = message.MessageMetadata["IsSystemMessage"].Value,
                _ = message.MessageMetadata.Select(x => CreateField("Query", x.Value))
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
                IsSystemMessage = message.MostRecentAttempt.MessageMetadata["IsSystemMessage"].Value,
                _ = message.MostRecentAttempt.MessageMetadata.Select(x => CreateField("Query", x.Value))
            }));


            Index("Query", FieldIndexing.Analyzed);
            Index(x => x.ProcessedAt, FieldIndexing.Default);

            Analyze("Query", typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesViewIndex.MergedMessage>
    {
        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata = message.MostRecentAttempt != null ? message.MostRecentAttempt.MessageMetadata : message.MessageMetadata
                                           select new
                                           {
                                               Id = message.UniqueMessageId,
                                               IsSystemMessage = metadata["IsSystemMessage"].Value,
                                               MessageId = metadata["MessageId"].Value,
                                               MessageType = metadata["MessageType"].Value,
                                               MessageIntent = metadata["MessageIntent"].Value,
                                               SendingEndpoint = metadata["SendingEndpoint"].Value,
                                               ReceivingEndpoint = metadata["ReceivingEndpoint"].Value,
                                               ProcessingTime = metadata["ProcessingTime"].Value,
                                               CriticalTime =  metadata["CriticalTime"].Value,
                                               BodyUrl = metadata["BodyUrl"].Value,
                                               BodySize = metadata["BodySize"].Value,
                                               Status = message.MostRecentAttempt != null ? MessageStatus.Failed : MessageStatus.Successful
                                           };
        }
    }
}