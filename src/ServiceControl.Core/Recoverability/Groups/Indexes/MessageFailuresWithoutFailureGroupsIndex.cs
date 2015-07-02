namespace ServiceControl.Recoverability.Groups.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.MessageFailures;

    public class MessageFailuresWithoutFailureGroupsIndex : AbstractIndexCreationTask<MessageFailureHistory>
    {
        public MessageFailuresWithoutFailureGroupsIndex()
        {
            Map = failures => from failure in failures
                              where (failure.Status == FailedMessageStatus.Unresolved && failure.FailureGroups == null)
                select new
                {
                   failure.Id
                };

        }
    }
}