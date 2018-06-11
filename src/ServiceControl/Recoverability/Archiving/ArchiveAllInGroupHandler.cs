namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();
        const int batchSize = 1000;

        IDocumentStore store;
        IDomainEvents domainEvents;
        ArchiveDocumentManager documentManager;
        ArchivingManager archiveOperationManager;
        RetryingManager retryingManager;

        public ArchiveAllInGroupHandler(IDocumentStore store, IDomainEvents domainEvents, ArchiveDocumentManager documentManager, ArchivingManager archiveOperationManager, RetryingManager retryingManager)
        {
            this.store = store;
            this.documentManager = documentManager;
            this.archiveOperationManager = archiveOperationManager;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
        }

        public void Handle(ArchiveAllInGroup message)
        {
            HandleAsync(message).GetAwaiter().GetResult();
        }

        private async Task HandleAsync(ArchiveAllInGroup message)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.Warn($"Attempt to archive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            logger.Info($"Archiving of {message.GroupId} started");
            ArchiveOperation archiveOperation;

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                archiveOperation = await documentManager.LoadArchiveOperation(session, message.GroupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                if (archiveOperation == null)
                {
                    var groupDetails = await documentManager.GetGroupDetails(session, message.GroupId).ConfigureAwait(false);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to archive in group {message.GroupId}");

                        return;
                    }

                    logger.Info($"Splitting group {message.GroupId} into batches");
                    archiveOperation = await documentManager.CreateArchiveOperation(session, message.GroupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize)
                        .ConfigureAwait(false);
                    await session.SaveChangesAsync();

                    logger.Info($"Group {message.GroupId} has been split into {archiveOperation.NumberOfBatches} batches");
                }
            }

            await archiveOperationManager.StartArchiving(archiveOperation)
                .ConfigureAwait(false);

            while (archiveOperation.CurrentBatch < archiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenAsyncSession())
                {
                    var nextBatch = await documentManager.GetArchiveBatch(batchSession, archiveOperation.Id, archiveOperation.CurrentBatch)
                        .ConfigureAwait(false);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.Warn($"Attempting to archive a batch ({archiveOperation.Id}/{archiveOperation.CurrentBatch}) which appears to already have been archived.");
                    }
                    else
                    {
                        logger.Info($"Archiving {nextBatch.DocumentIds.Count} messages from group {message.GroupId} starting");
                    }

                    await documentManager.ArchiveMessageGroupBatch(batchSession, nextBatch)
                        .ConfigureAwait(false);

                    await archiveOperationManager.BatchArchived(archiveOperation.RequestId, archiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0)
                        .ConfigureAwait(false);

                    archiveOperation = archiveOperationManager.GetStatusForArchiveOperation(archiveOperation.RequestId, archiveOperation.ArchiveType).ToArchiveOperation();

                    await documentManager.UpdateArchiveOperation(batchSession, archiveOperation);

                    await batchSession.SaveChangesAsync();

                    if (nextBatch != null)
                    {
                        logger.Info($"Archiving of {nextBatch.DocumentIds.Count} messages from group {message.GroupId} completed");
                    }
                }
            }

            logger.Info($"Archiving of group {message.GroupId} is complete. Waiting for index updates.");
            await archiveOperationManager.ArchiveOperationFinalizing(archiveOperation.RequestId, archiveOperation.ArchiveType)
                .ConfigureAwait(false);
            if (! await documentManager.WaitForIndexUpdateOfArchiveOperation(store, archiveOperation.RequestId, archiveOperation.ArchiveType, TimeSpan.FromMinutes(5))
                .ConfigureAwait(false))
            {
                logger.Warn($"Archiving group {message.GroupId} completed but index not updated.");
            }

            logger.Info($"Archiving of group {message.GroupId} completed");
            await archiveOperationManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType)
                .ConfigureAwait(false);
            await documentManager.RemoveArchiveOperation(store, archiveOperation).ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessageGroupArchived
            {
                GroupId = message.GroupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages
            });
        }
    }
}
