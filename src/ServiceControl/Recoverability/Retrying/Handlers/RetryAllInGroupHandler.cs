namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public async Task Handle(RetryAllInGroup message, IMessageHandlerContext context)
        {
            if (retries == null)
            {
                log.Warn($"Attempt to retry a group ({message.GroupId}) when retries are disabled");
                return;
            }

            if (archivingManager.IsArchiveInProgressFor(message.GroupId))
            {
                log.Warn($"Attempt to retry a group ({message.GroupId}) which is currently in the process of being archived");
                return;
            }

            FailureGroupView group;

            using (var session = store.OpenAsyncSession())
            {
                group = await session.Query<FailureGroupView, FailureGroupsViewIndex>()
                    .FirstOrDefaultAsync(x => x.Id == message.GroupId)
                    .ConfigureAwait(false);
            }

            string originator = null;
            if (group?.Title != null)
            {
                originator = group.Title;
            }

            var started = message.Started ?? DateTime.UtcNow;
            await retryingManager.Wait(message.GroupId, RetryType.FailureGroup, started, originator, group?.Type, group?.Last)
                .ConfigureAwait(false);
            retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetryType.FailureGroup, started, x => x.FailureGroupId == message.GroupId, originator, group?.Type);
        }

        public RetryAllInGroupHandler(RetriesGateway retries, IDocumentStore store, RetryingManager retryingManager, ArchivingManager archivingManager)
        {
            this.retries = retries;
            this.store = store;
            this.retryingManager = retryingManager;
            this.archivingManager = archivingManager;
        }

        readonly RetriesGateway retries;
        readonly IDocumentStore store;
        readonly RetryingManager retryingManager;
        readonly ArchivingManager archivingManager;
        static ILog log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}