namespace ServiceControl.Recoverability
{
    using System.Linq;
    using NServiceBus;
    using Raven.Client;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public void Handle(RetryAllInGroup message)
        {
            if (Retries == null)
            {
                return;
            }

            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                .FirstOrDefault(x => x.Id == message.GroupId);

            string context = null;

            if (group != null && group.Title != null)
            {
                context = group.Title;
            }

            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(x => x.FailureGroupId == message.GroupId, context);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentSession Session { get; set; }
    }
}