namespace ServiceControl.Recoverability
{
    using System.Linq;
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
                    MessageId = doc.UniqueMessageId,
                    FailureGroupId = failureGroup.Id,
                    Status = doc.Status
                };
        }
    }
}