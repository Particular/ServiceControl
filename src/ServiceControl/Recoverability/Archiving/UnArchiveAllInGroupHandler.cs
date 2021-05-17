namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;

    class UnarchiveAllInGroupHandler : IHandleMessages<UnarchiveAllInGroup>
    {
        public UnarchiveAllInGroupHandler(IDocumentStore store, IDomainEvents domainEvents, UnarchiveDocumentManager documentManager, UnarchivingManager unarchiveOperationManager, RetryingManager retryingManager)
        {
            this.store = store;
            this.documentManager = documentManager;
            this.unarchiveOperationManager = unarchiveOperationManager;
            this.retryingManager = retryingManager;
            this.domainEvents = domainEvents;
        }

        public async Task Handle(UnarchiveAllInGroup message, IMessageHandlerContext context)
        {
            if (retryingManager.IsRetryInProgressFor(message.GroupId))
            {
                logger.Warn($"Attempt to unarchive a group ({message.GroupId}) which is currently in the process of being retried");
                return;
            }

            logger.Info($"Unarchiving of {message.GroupId} started");
            UnarchiveOperation unarchiveOperation;

            using (var session = store.OpenAsyncSession())
            {
                session.Advanced.UseOptimisticConcurrency = true; // Ensure 2 messages don't split the same operation into batches at once

                unarchiveOperation = await documentManager.LoadUnarchiveOperation(session, message.GroupId, ArchiveType.FailureGroup)
                    .ConfigureAwait(false);

                if (unarchiveOperation == null)
                {
                    var groupDetails = await documentManager.GetGroupDetails(session, message.GroupId).ConfigureAwait(false);
                    if (groupDetails.NumberOfMessagesInGroup == 0)
                    {
                        logger.Warn($"No messages to unarchive in group {message.GroupId}");

                        return;
                    }

                    logger.Info($"Splitting group {message.GroupId} into batches");
                    unarchiveOperation = await documentManager.CreateUnarchiveOperation(session, message.GroupId, ArchiveType.FailureGroup, groupDetails.NumberOfMessagesInGroup, groupDetails.GroupName, batchSize)
                        .ConfigureAwait(false);
                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);

                    logger.Info($"Group {message.GroupId} has been split into {unarchiveOperation.NumberOfBatches} batches");
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
                        logger.Info($"Unarchiving {nextBatch.DocumentIds.Count} messages from group {message.GroupId} starting");
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
                        logger.Info($"Unarchiving of {nextBatch.DocumentIds.Count} messages from group {message.GroupId} completed");
                    }
                }
            }

            logger.Info($"Unarchiving of group {message.GroupId} is complete. Waiting for index updates.");
            await unarchiveOperationManager.UnarchiveOperationFinalizing(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType)
                .ConfigureAwait(false);
            if (!await documentManager.WaitForIndexUpdateOfUnarchiveOperation(store, unarchiveOperation.RequestId, TimeSpan.FromMinutes(5))
                .ConfigureAwait(false))
            {
                logger.Warn($"Unarchiving group {message.GroupId} completed but index not updated.");
            }

            logger.Info($"Unarchiving of group {message.GroupId} completed");
            await unarchiveOperationManager.UnarchiveOperationCompleted(unarchiveOperation.RequestId, unarchiveOperation.ArchiveType)
                .ConfigureAwait(false);
            await documentManager.RemoveUnarchiveOperation(store, unarchiveOperation).ConfigureAwait(false);

            await domainEvents.Raise(new FailedMessageGroupUnarchived
            {
                GroupId = message.GroupId,
                GroupName = unarchiveOperation.GroupName,
                MessagesCount = unarchiveOperation.TotalNumberOfMessages,
            }).ConfigureAwait(false);
        }

        IDocumentStore store;
        IDomainEvents domainEvents;
        UnarchiveDocumentManager documentManager;
        UnarchivingManager unarchiveOperationManager;
        RetryingManager retryingManager;
        const int batchSize = 1000;

        static ILog logger = LogManager.GetLogger<UnarchiveAllInGroupHandler>();
    }
}
