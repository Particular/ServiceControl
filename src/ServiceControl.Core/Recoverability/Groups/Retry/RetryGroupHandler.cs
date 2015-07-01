namespace ServiceControl.Recoverability.Groups.Retry
{
    using System;
    using System.Linq;
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

            Retryer.StartRetryForIndex<MessageFailuresByFailureGroupsIndex>(f => f.FailureGroups.Any(g => g.Id == message.GroupId));
        }

        public Retryer Retryer { get; set; }
    }
}
