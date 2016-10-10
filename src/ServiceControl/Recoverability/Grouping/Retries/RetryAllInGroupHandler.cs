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
                log.Warn($"Attempt to retry a group ({message.GroupId}) when retries are disabled");
                return;
            }

            FailureGroupView group;

            using (var session = Store.OpenSession())
            {
                group = session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .FirstOrDefault(x => x.Id == message.GroupId);
            }
            string context = null;

            if (@group?.Title != null)
            {
                context = group.Title;
            }

            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(x => x.FailureGroupId == message.GroupId, context);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentStore Store { get; set; }

        static ILog log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}