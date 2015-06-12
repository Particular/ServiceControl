namespace ServiceControl.Groups.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class MessageFailuresByFailureGroupsIndex : AbstractIndexCreationTask<MessageFailureHistory>
    {
        public MessageFailuresByFailureGroupsIndex()
        {
            Map = failures => from failure in failures
                              where failure.Status == FailedMessageStatus.Unresolved
                from failureGroup in failure.FailureGroups
                select new
                {
                    FailureGroups_Id = failureGroup.Id
                };

        }
    }
}