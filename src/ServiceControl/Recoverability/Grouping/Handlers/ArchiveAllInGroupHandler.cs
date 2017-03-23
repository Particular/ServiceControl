namespace ServiceControl.Recoverability
{
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using Raven.Abstractions.Commands;
    using System;

    public class ArchiveAllInGroupHandler : IHandleMessages<ArchiveAllInGroup>
    {
        private static ILog logger = LogManager.GetLogger<ArchiveAllInGroupHandler>();
        private const int batchSize = 1000;

        private readonly IBus bus;
        private readonly IDocumentStore store;
        private readonly ArchiveDocumentManager documentManager;
        private readonly OperationManager archiveOperationManager;

        public ArchiveAllInGroupHandler(IBus bus, IDocumentStore store, ArchiveDocumentManager documentManager, OperationManager archiveOperationManager)
        {
            this.bus = bus;
            this.store = store;
            this.documentManager = documentManager;
            this.archiveOperationManager = archiveOperationManager;
        }

        public void Handle(ArchiveAllInGroup message)
        {
            if (archiveOperationManager.IsRetryInProgressFor(message.GroupId))
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
                }
            }

            archiveOperationManager.StartArchiving(archiveOperation.RequestId, archiveOperation.ArchiveType, archiveOperation.TotalNumberOfMessages, archiveOperation.NumberOfMessagesArchived, archiveOperation.Started, archiveOperation.GroupName, archiveOperation.NumberOfBatches, archiveOperation.CurrentBatch);

            while (archiveOperation.CurrentBatch < archiveOperation.NumberOfBatches)
            {
                using (var batchSession = store.OpenSession())
                {
                    var nextBatch = documentManager.GetArchiveBatch(batchSession, archiveOperation.Id, archiveOperation.CurrentBatch);
                    if (nextBatch == null)
                    {
                        // We're only here in the case where Raven indexes are stale
                        break;
                    }

                    documentManager.ArchiveMessageGroupBatch(batchSession, nextBatch);

                    archiveOperationManager.BatchArchived(archiveOperation.RequestId, archiveOperation.ArchiveType, nextBatch.DocumentIds.Count);
                    archiveOperation = archiveOperationManager.GetStatusForArchiveOperation(archiveOperation.RequestId, archiveOperation.ArchiveType).ToArchiveOperation();

                    documentManager.UpdateArchiveOperation(batchSession, archiveOperation);

                    batchSession.SaveChanges();

                    logger.Info($"Archiving of {nextBatch.DocumentIds.Count} messages from group {message.GroupId} completed");
                }
            }

            logger.Info($"Archiving of group {message.GroupId} completed");
            archiveOperationManager.ArchiveOperationCompleted(archiveOperation.RequestId, archiveOperation.ArchiveType);
            documentManager.RemoveArchiveOperation(store, archiveOperation);

            bus.Publish(new FailedMessageGroupArchived
            {
                GroupId = message.GroupId,
                GroupName = archiveOperation.GroupName,
                MessagesCount = archiveOperation.TotalNumberOfMessages
            });
        }
    }
}
