namespace ServiceControl.Recoverability
{
    using System.Linq;
    using NServiceBus.Logging;
    using NServiceBus;
    using Raven.Client;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public void Handle(RetryAllInGroup message)
        {
            Logger.InfoFormat("Retry group: RetryAllInGroup received. GroupId {0}", message.GroupId);

            if (Retries == null)
            {
                Logger.Info("Retry group: Retries Gateway null, exiting");
                return;
            }

            Logger.Info("Retry group: Querying for failure group");
            var group = Session.Query<FailureGroupView, FailureGroupsViewIndex>()
                               .FirstOrDefault(x => x.Id == message.GroupId);

            string context = null;

            if (group != null && group.Title != null)
            {
                Logger.Info("Retry group: Queried group is null");
                context = group.Title;
            }
            else
            {
                Logger.InfoFormat("Retry group: Queried group returned with Id {0}", group.Id);
            }

            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(x => x.FailureGroupId == message.GroupId, context);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentSession Session { get; set; }

        static readonly ILog Logger = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}