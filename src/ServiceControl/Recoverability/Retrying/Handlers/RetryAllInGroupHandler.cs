namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    public class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public RetriesGateway Retries { get; set; }
        public IDocumentStore Store { get; set; }
        public RetryingManager RetryingManager { get; set; }
        public ArchivingManager ArchivingManager { get; set; }

        public async Task Handle(RetryAllInGroup message, IMessageHandlerContext context)
        {
            if (Retries == null)
            {
                log.Warn($"Attempt to retry a group ({message.GroupId}) when retries are disabled");
                return;
            }

            if (ArchivingManager.IsArchiveInProgressFor(message.GroupId))
            {
                log.Warn($"Attempt to retry a group ({message.GroupId}) which is currently in the process of being archived");
                return;
            }

            FailureGroupView group;

            using (var session = Store.OpenAsyncSession())
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
            await RetryingManager.Wait(message.GroupId, RetryType.FailureGroup, started, originator, group?.Type, group?.Last)
                .ConfigureAwait(false);
            Retries.StartRetryForIndex<FailureGroupMessageView, FailedMessages_ByGroup>(message.GroupId, RetryType.FailureGroup, started, x => x.FailureGroupId == message.GroupId, originator, group?.Type);
        }

        static ILog log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}