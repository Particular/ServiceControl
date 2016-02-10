namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class FailedMessages_ByGroup : AbstractIndexCreationTask<FailedMessage, FailureGroupMessageView>
    {
        public FailedMessages_ByGroup()
        {
            Map = docs => from doc in docs
                let metadata = doc.ProcessingAttempts.Last().MessageMetadata
                from failureGroup in doc.FailureGroups
                select new FailureGroupMessageView
                {
                    Id = doc.Id,
                    MessageId = doc.UniqueMessageId,
                    FailureGroupId = failureGroup.Id,
                    FailureGroupName = failureGroup.Title,
                    Status = doc.Status,
                    MessageType = (string) metadata["MessageType"],
                    TimeSent = (DateTime) metadata["TimeSent"]
                };

            StoreAllFields(FieldStorage.Yes);
            DisableInMemoryIndexing = true;
        }
    }
}