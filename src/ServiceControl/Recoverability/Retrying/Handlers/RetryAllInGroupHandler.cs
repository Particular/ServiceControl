namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Recoverability;

    class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public async Task Handle(RetryAllInGroup message, IMessageHandlerContext context)
        {
            if (retries == null)
            {
                Log.Warn($"Attempt to retry a group ({message.GroupId}) when retries are disabled");
                return;
            }

            if (archiver.IsArchiveInProgressFor(message.GroupId))
            {
                Log.Warn($"Attempt to retry a group ({message.GroupId}) which is currently in the process of being archived");
                return;
            }


            var group = await dataStore.QueryFailureGroupViewOnGroupId(message.GroupId);

            string originator = null;
            if (group?.Title != null)
            {
                originator = group.Title;
            }

            var started = message.Started ?? DateTime.UtcNow;
            await retryingManager.Wait(message.GroupId, RetryType.FailureGroup, started, originator, group?.Type, group?.Last);

            retries.EnqueueRetryForFailureGroup(new RetriesGateway.RetryForFailureGroup(
                message.GroupId,
                group.Title,
                group.Type,
                started
            ));
        }

        public RetryAllInGroupHandler(RetriesGateway retries, RetryingManager retryingManager, IArchiveMessages archiver, IRetryDocumentDataStore dataStore)
        {
            this.retries = retries;
            this.retryingManager = retryingManager;
            this.archiver = archiver;
            this.dataStore = dataStore;
        }

        readonly RetriesGateway retries;
        readonly RetryingManager retryingManager;
        readonly IArchiveMessages archiver;
        readonly IRetryDocumentDataStore dataStore;
        static readonly ILog Log = LogManager.GetLogger(typeof(RetryAllInGroupHandler));
    }
}