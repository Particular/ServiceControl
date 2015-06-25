namespace ServiceControl.Recoverability.Groups.Retry
{
    using System;
    using NServiceBus;
    using ServiceControl.Recoverability.Groups.Indexes;
    using ServiceControl.Recoverability.Retries;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public void Handle(RetryAllInGroup message)
        {
            if (String.IsNullOrWhiteSpace(message.GroupId))
            {
                return;
            }

            if (Retryer == null)
            {
                return;
            }

            var query = String.Format("FailureGroups_Id:{0}", message.GroupId);

            Retryer.StartRetryForIndex<MessageFailuresByFailureGroupsIndex>(message.BatchId, query);
        }

        public Retryer Retryer { get; set; }
    }
}
