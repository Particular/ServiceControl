namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using MessageFailures;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;

    public class FailedMessages_ByGroup : AbstractIndexCreationTask<FailedMessage, FailureGroupMessageView>
    {
        public FailedMessages_ByGroup()
        {
            Map = docs => from doc in docs
                let processingAttemptsLast = doc.ProcessingAttempts.Last()
                from failureGroup in doc.FailureGroups
                select new FailureGroupMessageView
                {
                    Id = doc.Id,
                    MessageId = doc.UniqueMessageId,
                    FailureGroupId = failureGroup.Id,
                    FailureGroupName = failureGroup.Title,
                    Status = doc.Status,
                    MessageType = (string)processingAttemptsLast.MessageMetadata["MessageType"],
                    TimeSent = (DateTime)processingAttemptsLast.MessageMetadata["TimeSent"],
                    TimeOfFailure = processingAttemptsLast.FailureDetails.TimeOfFailure,
                    LastModified = MetadataFor(doc).Value<DateTime>("Last-Modified").Ticks
                };

            StoreAllFields(FieldStorage.Yes);

            DisableInMemoryIndexing = true;
        }
    }
}