namespace ServiceControl.Recoverability
{
    using System.Linq;
    using Raven.Abstractions.Indexing;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class FailedMessages_ByGroup : AbstractIndexCreationTask<FailedMessage, FailureGroupMessageView>
    {
        public FailedMessages_ByGroup()
        {
            Map = docs => from doc in docs
                from failureGroup in doc.FailureGroups
                select new FailureGroupMessageView
                {
                    Id = doc.Id, 
                    MessageId = doc.UniqueMessageId,
                    FailureGroupId = failureGroup.Id,
                    FailureGroupName = failureGroup.Title,
                    Status = doc.Status
                };

            StoreAllFields(FieldStorage.Yes);
        }
    }
}