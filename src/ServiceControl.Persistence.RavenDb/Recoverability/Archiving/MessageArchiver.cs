namespace ServiceControl.Persistence.RavenDb.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Persistence.Recoverability;
    using ServiceControl.Recoverability;

    class MessageArchiver : IArchiveMessages
    {
        public MessageArchiver(IDocumentStore store, OperationsManager operationsManager)
        {
            this.store = store;
            this.operationsManager = operationsManager;
        }

        public async Task ArchiveAllInGroup(string groupId, IDomainEvents domainEvents)
        {
            logger.Info($"Archiving of {groupId} started");
            ArchiveOperation archiveOperation;

            var documentManager = new ArchiveDocumentManager();
            var archiveOperationManager = new ArchivingManager(domainEvents, operationsManager);

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                archiveOperation = await documentManager.LoadArchiveOperation(session, groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                if (archiveOperation == null)
                {
                    var groupDetails = await documentManager.GetGroupDetails(session, groupId).ConfigureAwait(false);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to archive in group {groupId}");
                        return;
                    }

                    logger.Info($"Splitting group {groupId} into batches");
                    archiveOperation = await documentManager.CreateArchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize)
                        .ConfigureAwait(false);
                    await session.SaveChangesAsync()
                    .ConfigureAwait(false);

                    logger.Info($"Group {groupId} has been split into {archiveOperation.NumberOfBatches} batches");
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
                        logger.Info($"Archiving {nextBatch.DocumentIds.Count} messages from group {groupId} starting");
                    }

                    await documentManager.ArchiveMessageGroupBatch(batchSession, nextBatch)
                        .ConfigureAwait(false);

                    await archiveOperationManager.BatchArchived(archiveOperation.RequestId, archiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0)
                        .ConfigureAwait(false);

                    archiveOperation = archiveOperationManager.GetStatusForArchiveOperation(archiveOperation.RequestId, archiveOperation.ArchiveType).ToArchiveOperation();

                    await documentManager.UpdateArchiveOperation(batchSession, archiveOperation)
                        .ConfigureAwait(false);

                    await batchSession.SaveChangesAsync()
                        .ConfigureAwait(false);

                    if (nextBatch != null)
                    {
                        await domainEvents.Raise(new FailedMessageGroupBatchArchived
                        {
                            // Remove `FailedMessages/` prefix and publish pure GUIDs without Raven collection name
                            FailedMessagesIds = nextBatch.DocumentIds.Select(id => id.Replace("FailedMessages/", "")).ToArray()
                        }).ConfigureAwait(false);
                    }

                    if (nextBatch != null)
                    {
                        logger.Info($"Archiving of {nextBatch.DocumentIds.Count} messages from group {groupId} completed");
                    }
                }
            }

            logger.Info($"Archiving of group {groupId} is complete. Waiting for index updates.");
            await archiveOperationManager.ArchiveOperationFinalizing(archiveOperation.RequestId, archiveOperation.ArchiveType)
                .ConfigureAwait(false);
            if (!await documentManager.WaitForIndexUpdateOfArchiveOperation(store, archiveOperation.RequestId, TimeSpan.FromMinutes(5))
                .ConfigureAwait(false))
            {
                logger.Warn($"Archiving group {groupId} completed but index not updated.");
            }

            await archiveOperationManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType)
                .ConfigureAwait(false);
            await documentManager.RemoveArchiveOperation(store, archiveOperation).ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessageGroupArchived
            {
                GroupId = groupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages,
            }).ConfigureAwait(false);

            logger.Info($"Archiving of group {groupId} completed");
        }

        public async Task UnarchiveAllInGroup(string groupId, IDomainEvents domainEvents)
        {
            logger.Info($"Unarchiving of {groupId} started");
            UnarchiveOperation unarchiveOperation;

            var documentManager = new UnarchiveDocumentManager();
            var unarchiveOperationManager = new UnarchivingManager(domainEvents, operationsManager);

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                unarchiveOperation = await documentManager.LoadUnarchiveOperation(session, groupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                if (unarchiveOperation == null)
                {
                    var groupDetails = await documentManager.GetGroupDetails(session, groupId).ConfigureAwait(false);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to unarchive in group {groupId}");

                        return;
                    }

                    logger.Info($"Splitting group {groupId} into batches");
                    unarchiveOperation = await documentManager.CreateUnarchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize)
                        .ConfigureAwait(false);
                    await session.SaveChangesAsync()
                    .ConfigureAwait(false);

                    logger.Info($"Group {groupId} has been split into {unarchiveOperation.NumberOfBatches} batches");
                }
            }

            await unarchiveOperationManager.StartUnarchiving(unarchiveOperation)
                .ConfigureAwait(false);

            while (unarchiveOperation.CurrentBatch < unarchiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenAsyncSession())
                {
                    var nextBatch = await documentManager.GetUnarchiveBatch(batchSession, unarchiveOperation.Id, unarchiveOperation.CurrentBatch)
                        .ConfigureAwait(false);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.Warn($"Attempting to unarchive a batch ({unarchiveOperation.Id}/{unarchiveOperation.CurrentBatch}) which appears to already have been archived.");
                    }
                    else
                    {
                        logger.Info($"Unarchiving {nextBatch.DocumentIds.Count} messages from group {groupId} starting");
                    }

                    await documentManager.UnarchiveMessageGroupBatch(batchSession, nextBatch)
                        .ConfigureAwait(false);

                    await unarchiveOperationManager.BatchUnarchived(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0)
                        .ConfigureAwait(false);

                    unarchiveOperation = unarchiveOperationManager.GetStatusForUnarchiveOperation(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType).ToUnarchiveOperation();

                    await documentManager.UpdateUnarchiveOperation(batchSession, unarchiveOperation)
                        .ConfigureAwait(false);

                    await batchSession.SaveChangesAsync()
                        .ConfigureAwait(false);

                    if (nextBatch != null)
                    {
                        await domainEvents.Raise(new FailedMessageGroupBatchUnarchived
                        {
                            // Remove `FailedMessages/` prefix and publish pure GUIDs without Raven collection name
                            FailedMessagesIds = nextBatch.DocumentIds.Select(id => id.Replace("FailedMessages/", "")).ToArray()
                        }).ConfigureAwait(false);
                    }

                    if (nextBatch != null)
                    {
                        logger.Info($"Unarchiving of {nextBatch.DocumentIds.Count} messages from group {groupId} completed");
                    }
                }
            }

            logger.Info($"Unarchiving of group {groupId} is complete. Waiting for index updates.");
            await unarchiveOperationManager.UnarchiveOperationFinalizing(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType)
                .ConfigureAwait(false);
            if (!await documentManager.WaitForIndexUpdateOfUnarchiveOperation(store, unarchiveOperation.RequestId, TimeSpan.FromMinutes(5))
                .ConfigureAwait(false))
            {
                logger.Warn($"Unarchiving group {groupId} completed but index not updated.");
            }

            logger.Info($"Unarchiving of group {groupId} completed");
            await unarchiveOperationManager.UnarchiveOperationCompleted(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType)
                .ConfigureAwait(false);
            await documentManager.RemoveUnarchiveOperation(store, unarchiveOperation).ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessageGroupUnarchived
            {
                GroupId = groupId,
                GroupName = unarchiveOperation.GroupName,
                MessagesCount = unarchiveOperation.TotalNumberOfMessages,
            }).ConfigureAwait(false);
        }

        public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => throw new NotImplementedException();
        public Task StartArchiving(string groupId, ArchiveType archiveType) => throw new NotImplementedException();

        readonly IDocumentStore store;
        readonly OperationsManager operationsManager;
        static ILog logger = LogManager.GetLogger<MessageArchiver>();
        const int batchSize = 1000;
    }
}
