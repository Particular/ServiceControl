namespace ServiceControl.Persistence.RavenDb.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Documents;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Persistence.Recoverability;
    using ServiceControl.Recoverability;

    class MessageArchiver : IArchiveMessages
    {
        public MessageArchiver(IDocumentStore store, OperationsManager operationsManager, IDomainEvents domainEvents)
        {
            this.store = store;
            this.domainEvents = domainEvents;
            this.operationsManager = operationsManager;

            archiveDocumentManager = new ArchiveDocumentManager();
            archivingManager = new ArchivingManager(domainEvents, operationsManager);

            unarchiveDocumentManager = new UnarchiveDocumentManager();
            unarchivingManager = new UnarchivingManager(domainEvents, operationsManager);
        }

        public async Task ArchiveAllInGroup(string groupId)
        {
            logger.Info($"Archiving of {groupId} started");
            ArchiveOperation archiveOperation;

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                archiveOperation = await archiveDocumentManager.LoadArchiveOperation(session, groupId, ArchiveType.FailureGroup);

                if (archiveOperation == null)
                {
                    var groupDetails = await archiveDocumentManager.GetGroupDetails(session, groupId);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to archive in group {groupId}");
                        return;
                    }

                    logger.Info($"Splitting group {groupId} into batches");
                    archiveOperation = await archiveDocumentManager.CreateArchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize);
                    await session.SaveChangesAsync();

                    logger.Info($"Group {groupId} has been split into {archiveOperation.NumberOfBatches} batches");
                }
            }

            await archivingManager.StartArchiving(archiveOperation);

            while (archiveOperation.CurrentBatch < archiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenAsyncSession())
                {
                    var nextBatch = await archiveDocumentManager.GetArchiveBatch(batchSession, archiveOperation.Id, archiveOperation.CurrentBatch);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.Warn($"Attempting to archive a batch ({archiveOperation.Id}/{archiveOperation.CurrentBatch}) which appears to already have been archived.");
                    }
                    else
                    {
                        logger.Info($"Archiving {nextBatch.DocumentIds.Count} messages from group {groupId} starting");
                    }

                    await archiveDocumentManager.ArchiveMessageGroupBatch(batchSession, nextBatch);

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
                        logger.Info($"Archiving of {nextBatch.DocumentIds.Count} messages from group {groupId} completed");
                    }
                }
            }

            logger.Info($"Archiving of group {groupId} is complete. Waiting for index updates.");
            await archivingManager.ArchiveOperationFinalizing(archiveOperation.RequestId, archiveOperation.ArchiveType);
            if (!await archiveDocumentManager.WaitForIndexUpdateOfArchiveOperation(store, archiveOperation.RequestId, TimeSpan.FromMinutes(5))
                )
            {
                logger.Warn($"Archiving group {groupId} completed but index not updated.");
            }

            await archivingManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType);
            await archiveDocumentManager.RemoveArchiveOperation(store, archiveOperation);

            await domainEvents.Raise(new FailedMessageGroupArchived
            {
                GroupId = groupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages,
            });

            logger.Info($"Archiving of group {groupId} completed");
        }

        public async Task UnarchiveAllInGroup(string groupId)
        {
            logger.Info($"Unarchiving of {groupId} started");
            UnarchiveOperation unarchiveOperation;

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                unarchiveOperation = await unarchiveDocumentManager.LoadUnarchiveOperation(session, groupId, ArchiveType.FailureGroup);

                if (unarchiveOperation == null)
                {
                    var groupDetails = await unarchiveDocumentManager.GetGroupDetails(session, groupId);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to unarchive in group {groupId}");

                        return;
                    }

                    logger.Info($"Splitting group {groupId} into batches");
                    unarchiveOperation = await unarchiveDocumentManager.CreateUnarchiveOperation(session, groupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize);
                    await session.SaveChangesAsync();

                    logger.Info($"Group {groupId} has been split into {unarchiveOperation.NumberOfBatches} batches");
                }
            }

            await unarchivingManager.StartUnarchiving(unarchiveOperation);

            while (unarchiveOperation.CurrentBatch < unarchiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenAsyncSession())
                {
                    var nextBatch = await unarchiveDocumentManager.GetUnarchiveBatch(batchSession, unarchiveOperation.Id, unarchiveOperation.CurrentBatch);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        logger.Warn($"Attempting to unarchive a batch ({unarchiveOperation.Id}/{unarchiveOperation.CurrentBatch}) which appears to already have been archived.");
                    }
                    else
                    {
                        logger.Info($"Unarchiving {nextBatch.DocumentIds.Count} messages from group {groupId} starting");
                    }

                    await unarchiveDocumentManager.UnarchiveMessageGroupBatch(batchSession, nextBatch);

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
                        logger.Info($"Unarchiving of {nextBatch.DocumentIds.Count} messages from group {groupId} completed");
                    }
                }
            }

            logger.Info($"Unarchiving of group {groupId} is complete. Waiting for index updates.");
            await unarchivingManager.UnarchiveOperationFinalizing(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType);
            if (!await unarchiveDocumentManager.WaitForIndexUpdateOfUnarchiveOperation(store, unarchiveOperation.RequestId, TimeSpan.FromMinutes(5))
                )
            {
                logger.Warn($"Unarchiving group {groupId} completed but index not updated.");
            }

            logger.Info($"Unarchiving of group {groupId} completed");
            await unarchivingManager.UnarchiveOperationCompleted(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType);
            await unarchiveDocumentManager.RemoveUnarchiveOperation(store, unarchiveOperation);

            await domainEvents.Raise(new FailedMessageGroupUnarchived
            {
                GroupId = groupId,
                GroupName = unarchiveOperation.GroupName,
                MessagesCount = unarchiveOperation.TotalNumberOfMessages,
            });
        }

        public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType)
        {
            return operationsManager.IsOperationInProgressFor(groupId, archiveType);
        }

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

        readonly IDocumentStore store;
        readonly OperationsManager operationsManager;
        readonly IDomainEvents domainEvents;
        readonly ArchiveDocumentManager archiveDocumentManager;
        readonly ArchivingManager archivingManager;
        readonly UnarchiveDocumentManager unarchiveDocumentManager;
        readonly UnarchivingManager unarchivingManager;
        static readonly ILog logger = LogManager.GetLogger<MessageArchiver>();
        const int batchSize = 1000;
    }
}
