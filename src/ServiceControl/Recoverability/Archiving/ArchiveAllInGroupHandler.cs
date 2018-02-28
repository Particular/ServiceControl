namespace ServiceControl.Recoverability
{
    using System;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        private static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();
        private const int batchSize = 1000;

        private readonly IDocumentStore store;
        private readonly ArchiveDocumentManager documentManager;
        private readonly ArchivingManager archiveOperationManager;
        private readonly RetryingManager retryingManager;

        public ArchiveAllInGroupHandler(IDocumentStore store, ArchiveDocumentManager documentManager, ArchivingManager archiveOperationManager, RetryingManager retryingManager)
        {
            this.store = store;
            this.documentManager = documentManager;
            this.archiveOperationManager = archiveOperationManager;
            this.retryingManager = retryingManager;
        }

        public void Handle(ArchiveAllInGroup message)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.Warn($"Attempt to archive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            logger.Info($"Archiving of {message.GroupId} started");
            ArchiveOperation archiveOperation;

            using (var session = store.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                archiveOperation = documentManager.LoadArchiveOperation(session, message.GroupId, ArchiveType.FailureGroup);

                if (archiveOperation == null)
                {
                    var groupDetails = documentManager.GetGroupDetails(session, message.GroupId);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to archive in group {message.GroupId}");

                        return;
                    }

                    logger.Info($"Splitting group {message.GroupId} into batches");
                    archiveOperation = documentManager.CreateArchiveOperation(session, message.GroupId, ArchiveType.FailureGroup, message.CutOff, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize);
                    session.SaveChanges();

                    logger.Info($"Group {message.GroupId} has been split into {archiveOperation.NumberOfBatches} batches");
                }
            }

            archiveOperationManager.StartArchiving(archiveOperation);

            while (archiveOperation.CurrentBatch < archiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenSession())
                {
                    var nextBatch = documentManager.GetArchiveBatch(batchSession, archiveOperation.Id, archiveOperation.CurrentBatch);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.Warn($"Attempting to archive a batch ({archiveOperation.Id}/{archiveOperation.CurrentBatch}) which appears to already have been archived.");
                    }
                    else
                    {
                        logger.Info($"Archiving {nextBatch.DocumentIds.Count} messages from group {message.GroupId} starting");
                    }

                    documentManager.ArchiveMessageGroupBatch(batchSession, nextBatch);

                    archiveOperationManager.BatchArchived(archiveOperation.RequestId, archiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0);

                    archiveOperation = archiveOperationManager.GetStatusForArchiveOperation(archiveOperation.RequestId, archiveOperation.ArchiveType).ToArchiveOperation();

                    documentManager.UpdateArchiveOperation(batchSession, archiveOperation);

                    batchSession.SaveChanges();

                    if (nextBatch != null)
                    {
                        logger.Info($"Archiving of {nextBatch.DocumentIds.Count} messages from group {message.GroupId} completed");
                    }
                }
            }

            logger.Info($"Archiving of group {message.GroupId} is complete. Waiting for index updates.");
            archiveOperationManager.ArchiveOperationFinalizing(archiveOperation.RequestId, archiveOperation.ArchiveType);
            if (!documentManager.WaitForIndexUpdateOfArchiveOperation(store, archiveOperation.RequestId, archiveOperation.ArchiveType, TimeSpan.FromMinutes(5)))
            {
                logger.Warn($"Archiving group {message.GroupId} completed but index not updated.");
            }

            logger.Info($"Archiving of group {message.GroupId} completed");
            archiveOperationManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType);
            documentManager.RemoveArchiveOperation(store, archiveOperation);

            DomainEvents.Raise(new FailedMessageGroupArchived
            {
                GroupId = message.GroupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages
            });
        }
    }
}
