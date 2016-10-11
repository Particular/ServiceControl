namespace ServiceControl.Recoverability
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public void Handle(RetryAllInGroup message)
        {
            if (Retries == null)
            {
                log.WarnFormat("Attempt to retry a group ({0}) when retries are disabled", message.GroupId);
                return;
            }

            var retryOperation = Session.Load<RetryOperation>(RetryOperation.MakeDocumentId(message.GroupId, RetriesGateway.RetryType.FailureGroup));
            if (retryOperation != null)
            {
                // Retrying a group that is already in progress.
                log.WarnFormat("Attempt to retry a group ({0}) that is already scheduled for retry", message.GroupId);
                return;
            }

            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                               .FirstOrDefault(x => x.Id == message.GroupId);

            string context = null;

            if (group != null && group.Title != null)
            {
                context = group.Title;
            }

            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetriesGateway.RetryType.FailureGroup, x => x.FailureGroupId == message.GroupId, context);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentSession Session { get; set; }

        static ILog log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}