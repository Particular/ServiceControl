namespace ServiceControl.Persistence.RavenDB.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using RavenDB;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Persistence.Recoverability;
    using ServiceControl.Recoverability;

    class MessageArchiver : IArchiveMessages
    {
        public MessageArchiver(
            IRavenSessionProvider sessionProvider,
            OperationsManager operationsManager,
            IDomainEvents domainEvents,
            ExpirationManager expirationManager,
            ILogger<MessageArchiver> logger
            )
        {
            this.sessionProvider = sessionProvider;
            this.domainEvents = domainEvents;
            this.expirationManager = expirationManager;
            this.logger = logger;
            this.operationsManager = operationsManager;

            archiveDocumentManager = new ArchiveDocumentManager(expirationManager, logger);
            archivingManager = new ArchivingManager(domainEvents, operationsManager);

            unarchiveDocumentManager = new UnarchiveDocumentManager();
            unarchivingManager = new UnarchivingManager(domainEvents, operationsManager);
        }

        public async Task ArchiveAllInGroup(string groupId)
        {
            logger.LogInformation("Archiving of {GroupId} started", groupId);
            ArchiveOperation archiveOperation;

            using (var session = await sessionProvider.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                archiveOperation = await archiveDocumentManager.LoadArchiveOperation(session, groupId, ArchiveType.FailureGroup);

                if (archiveOperation == null)
                {
                    var groupDetails = await archiveDocumentManager.GetGroupDetails(session, groupId);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.LogWarning("No messages to archive in group {GroupId}", groupId);
                        return;
                    }

                    logger.LogInformation("Splitting group {GroupId} into batches", groupId);
                    archiveOperation = await archiveDocumentManager.CreateArchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize);
                    await session.SaveChangesAsync();

                    logger.LogInformation("Group {GroupId} has been split into {NumberOfBatches} batches", groupId, archiveOperation.NumberOfBatches);
                }
            }

            await archivingManager.StartArchiving(archiveOperation);

            while (archiveOperation.CurrentBatch < archiveOperation.NumberOfBatches)
            {
                using (var batchSession = await sessionProvider.OpenSession())
                {
                    var nextBatch = await archiveDocumentManager.GetArchiveBatch(batchSession, archiveOperation.Id, archiveOperation.CurrentBatch);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.LogWarning("Attempting to archive a batch ({ArchiveOperationId}/{ArchiveOperationCurrentBatch}) which appears to already have been archived", archiveOperation.Id, archiveOperation.CurrentBatch);
                    }
                    else
                    {
                        logger.LogInformation("Archiving {MessageCount} messages from group {GroupId} starting", nextBatch.DocumentIds.Count, groupId);
                    }

                    archiveDocumentManager.ArchiveMessageGroupBatch(batchSession, nextBatch);

                    await archivingManager.BatchArchived(archiveOperation.RequestId, archiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0);

                    archiveOperation = archivingManager.GetStatusForArchiveOperation(archiveOperation.RequestId, archiveOperation.ArchiveType).ToArchiveOperation();

                    await archiveDocumentManager.UpdateArchiveOperation(batchSession, archiveOperation);

                    await batchSession.SaveChangesAsync();

                    if (nextBatch != null)
                    {
                        await domainEvents.Raise(new FailedMessageGroupBatchArchived
                        {
                            // Remove `FailedMessages/` prefix and publish pure GUIDs without Raven collection name
                            FailedMessagesIds = nextBatch.DocumentIds.Select(id => id.Replace("FailedMessages/", "")).ToArray()
                        });
                    }

                    if (nextBatch != null)
                    {
                        logger.LogInformation("Archiving of {MessageCount} messages from group {GroupId} completed", nextBatch.DocumentIds.Count, groupId);
                    }
                }
            }

            logger.LogInformation("Archiving of group {GroupId} is complete. Waiting for index updates", groupId);
            await archivingManager.ArchiveOperationFinalizing(archiveOperation.RequestId, archiveOperation.ArchiveType);
            if (!await archiveDocumentManager.WaitForIndexUpdateOfArchiveOperation(sessionProvider, archiveOperation.RequestId, TimeSpan.FromMinutes(5))
                )
            {
                logger.LogWarning("Archiving group {GroupId} completed but index not updated", groupId);
            }

            await archivingManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType);
            await archiveDocumentManager.RemoveArchiveOperation(sessionProvider, archiveOperation);

            await domainEvents.Raise(new FailedMessageGroupArchived
            {
                GroupId = groupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages,
            });

            logger.LogInformation("Archiving of group {GroupId} completed", groupId);
        }

        public async Task UnarchiveAllInGroup(string groupId)
        {
            logger.LogInformation("Unarchiving of {GroupId} started", groupId);
            UnarchiveOperation unarchiveOperation;

            using (var session = await sessionProvider.OpenSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                unarchiveOperation = await unarchiveDocumentManager.LoadUnarchiveOperation(session, groupId, ArchiveType.FailureGroup);

                if (unarchiveOperation == null)
                {
                    var groupDetails = await unarchiveDocumentManager.GetGroupDetails(session, groupId);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.LogWarning("No messages to unarchive in group {GroupId}", groupId);

                        return;
                    }

                    logger.LogInformation("Splitting group {GroupId} into batches", groupId);
                    unarchiveOperation = await unarchiveDocumentManager.CreateUnarchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize);
                    await session.SaveChangesAsync();

                    logger.LogInformation("Group {GroupId} has been split into {NumberOfBatches} batches", groupId, unarchiveOperation.NumberOfBatches);
                }
            }

            await unarchivingManager.StartUnarchiving(unarchiveOperation);

            while (unarchiveOperation.CurrentBatch < unarchiveOperation.NumberOfBatches)
            {
                using var batchSession = await sessionProvider.OpenSession();
                var nextBatch = await unarchiveDocumentManager.GetUnarchiveBatch(batchSession, unarchiveOperation.Id, unarchiveOperation.CurrentBatch);
                if (nextBatch == null)
                {
                    // We're only here in the case where Raven indexes are stale
                    logger.LogWarning("Attempting to unarchive a batch ({UnarchiveOperationId}/{UnarchiveOperationCurrentBatch}) which appears to already have been archived", unarchiveOperation.Id, unarchiveOperation.CurrentBatch);
                }
                else
                {
                    logger.LogInformation("Unarchiving {MessageCount} messages from group {GroupId} starting", nextBatch.DocumentIds.Count, groupId);
                }

                unarchiveDocumentManager.UnarchiveMessageGroupBatch(batchSession, nextBatch, expirationManager);

                await unarchivingManager.BatchUnarchived(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType, nextBatch?.DocumentIds.Count ?? 0);

                unarchiveOperation = unarchivingManager.GetStatusForUnarchiveOperation(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType).ToUnarchiveOperation();

                await unarchiveDocumentManager.UpdateUnarchiveOperation(batchSession, unarchiveOperation);

                await batchSession.SaveChangesAsync();

                if (nextBatch != null)
                {
                    await domainEvents.Raise(new FailedMessageGroupBatchUnarchived
                    {
                        // Remove `FailedMessages/` prefix and publish pure GUIDs without Raven collection name
                        FailedMessagesIds = nextBatch.DocumentIds.Select(id => id.Replace("FailedMessages/", "")).ToArray()
                    });
                }

                if (nextBatch != null)
                {
                    logger.LogInformation("Unarchiving of {MessageCount} messages from group {GroupId} completed", nextBatch.DocumentIds.Count, groupId);
                }
            }

            logger.LogInformation("Unarchiving of group {GroupId} is complete. Waiting for index updates", groupId);
            await unarchivingManager.UnarchiveOperationFinalizing(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType);
            if (!await unarchiveDocumentManager.WaitForIndexUpdateOfUnarchiveOperation(sessionProvider, unarchiveOperation.RequestId, TimeSpan.FromMinutes(5))
                )
            {
                logger.LogWarning("Unarchiving group {GroupId} completed but index not updated", groupId);
            }

            logger.LogInformation("Unarchiving of group {GroupId} completed", groupId);
            await unarchivingManager.UnarchiveOperationCompleted(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType);
            await unarchiveDocumentManager.RemoveUnarchiveOperation(sessionProvider, unarchiveOperation);

            await domainEvents.Raise(new FailedMessageGroupUnarchived
            {
                GroupId = groupId,
                GroupName = unarchiveOperation.GroupName,
                MessagesCount = unarchiveOperation.TotalNumberOfMessages,
            });
        }

        public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType) => operationsManager.IsOperationInProgressFor(groupId, archiveType);

        public bool IsArchiveInProgressFor(string groupId)
            => archivingManager.IsArchiveInProgressFor(groupId);

        public void DismissArchiveOperation(string groupId, ArchiveType archiveType)
            => archivingManager.DismissArchiveOperation(groupId, archiveType);

        public Task StartArchiving(string groupId, ArchiveType archiveType)
            => archivingManager.StartArchiving(groupId, archiveType);

        public Task StartUnarchiving(string groupId, ArchiveType archiveType)
            => unarchivingManager.StartUnarchiving(groupId, archiveType);

        public IEnumerable<InMemoryArchive> GetArchivalOperations()
            => archivingManager.GetArchivalOperations();

        readonly IRavenSessionProvider sessionProvider;
        readonly OperationsManager operationsManager;
        readonly IDomainEvents domainEvents;
        readonly ExpirationManager expirationManager;
        readonly ArchiveDocumentManager archiveDocumentManager;
        readonly ArchivingManager archivingManager;
        readonly UnarchiveDocumentManager unarchiveDocumentManager;
        readonly UnarchivingManager unarchivingManager;
        readonly ILogger<MessageArchiver> logger;
        const int batchSize = 1000;
    }
}
