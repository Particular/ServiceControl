namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Recoverability;

    class RetryAllInGroupHandler : IHandleMessages<RetryAllInGroup>
    {
        public async Task Handle(RetryAllInGroup message, IMessageHandlerContext context)
        {
            if (retries == null)
            {
                logger.LogWarning("Attempt to retry a group ({messageGroupId}) when retries are disabled", message.GroupId);
                return;
            }

            if (archiver.IsArchiveInProgressFor(message.GroupId))
            {
                logger.LogWarning("Attempt to retry a group ({messageGroupId}) which is currently in the process of being archived", message.GroupId);
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
                originator,
                group?.Type,
                started
            ));
        }

        public RetryAllInGroupHandler(RetriesGateway retries, RetryingManager retryingManager, IArchiveMessages archiver, IRetryDocumentDataStore dataStore, ILogger<RetryAllInGroupHandler> logger)
        {
            this.retries = retries;
            this.retryingManager = retryingManager;
            this.archiver = archiver;
            this.dataStore = dataStore;
            this.logger = logger;
        }

        readonly RetriesGateway retries;
        readonly RetryingManager retryingManager;
        readonly IArchiveMessages archiver;
        readonly IRetryDocumentDataStore dataStore;
        readonly ILogger<RetryAllInGroupHandler> logger;
    }
}