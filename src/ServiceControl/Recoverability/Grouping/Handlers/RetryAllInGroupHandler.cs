namespace ServiceControl.Recoverability
{
    using System;
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

            string originator = null;
            if (@group?.Title != null)
            {
                originator = group.Title;
            }
           
            var started = message.Started ?? DateTime.UtcNow;
            RetryOperationManager.Wait(message.GroupId, RetryType.FailureGroup, started, originator, group?.Type, group?.Last);
            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetryType.FailureGroup, started, x => x.FailureGroupId == message.GroupId, originator, group?.Type);
        }

        public RetriesGateway Retries { get; set; }
        public IDocumentStore Store { get; set; }
		public OperationManager RetryOperationManager { get; set; }
        static ILog log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}