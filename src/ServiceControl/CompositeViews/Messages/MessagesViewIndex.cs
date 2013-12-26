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

    public class MessagesIndex
    {
        public string Id { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
    public class MessagesViewIndex : AbstractMultiMapIndexCreationTask<MessagesIndex>
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
                _ = message.MessageMetadata.Select(x => CreateField("Query", x.Value))
            }));


            AddMap<FailedMessage>(messages => messages.Select(message => new
            {
                message.UniqueMessageId,
                MessageId = message.MostRecentAttempt.MessageMetadata["MessageId"],
                MessageType = message.MostRecentAttempt.MessageMetadata["MessageType"],
                MessageIntent = message.MostRecentAttempt.MessageMetadata["MessageIntent"],
                IsSystemMessage = message.MostRecentAttempt.MessageMetadata["IsSystemMessage"],
                Status = message.ProcessingAttempts.Count() == 1 ? MessageStatus.Failed : MessageStatus.RepeatedFailure,
                ProcessedAt = message.MostRecentAttempt.FailureDetails.TimeOfFailure,
                _ = message.MostRecentAttempt.MessageMetadata.Select(x => CreateField("Query", x.Value))
            }));


            Index("Query", FieldIndexing.Analyzed);
            Index(x => x.ProcessedAt, FieldIndexing.Default);

            Analyze("Query", typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesIndex>
    {
        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                                         select new
                                         {
                                             message.Id
                                         };
        }
    }
  
}