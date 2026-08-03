namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

public class MessageArchiver : IArchiveMessages
{
    public MessageArchiver(
        IServiceScopeFactory scopeFactory,
        OperationsManager operationsManager,
        IDomainEvents domainEvents,
        IMessageActionAuditLog auditLog,
        ILogger<MessageArchiver> logger
    )
    {
        this.scopeFactory = scopeFactory;
        this.operationsManager = operationsManager;
        this.auditLog = auditLog;
        this.logger = logger;
        this.domainEvents = domainEvents;

        archivingManager = new EFCoreArchivingManager(domainEvents, operationsManager);
        unarchivingManager = new EFCoreUnarchivingManager(domainEvents, operationsManager);
    }

    public async Task ArchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null)
    {
        logger.LogInformation("Archiving of {GroupId} started", groupId);

        ArchiveOperationEntity? operationEntity;
        AuditUser auditUser;
        string? auditOperationId;

        // ── Load-or-create operation row ──
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            operationEntity = await dbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, true);

            if (operationEntity != null)
            {
                // Resume scenario: operation already exists from a previous (possibly crashed) run
                logger.LogInformation("Resuming archive operation for group {GroupId} at batch {CurrentBatch}/{NumberOfBatches}", groupId, operationEntity.CurrentBatch, operationEntity.NumberOfBatches);
            }
            else
            {
                // New operation: get group details
                var (count, groupName) = await ArchiveQueryHelper.GetGroupDetails(dbContext, groupId, FailedMessageStatus.Unresolved);

                if (count == 0)
                {
                    logger.LogWarning("No messages to archive in group {GroupId}", groupId);
                    return;
                }

                operationEntity = new ArchiveOperationEntity
                {
                    RequestId = groupId,
                    GroupName = groupName,
                    ArchiveType = ArchiveType.FailureGroup,
                    IsArchive = true,
                    TotalNumberOfMessages = count,
                    NumberOfMessagesProcessed = 0,
                    NumberOfBatches = (int)Math.Ceiling(count / (float)batchSize),
                    CurrentBatch = 0,
                    Started = DateTime.UtcNow,
                    InitiatedById = initiatedBy?.Id,
                    InitiatedByName = initiatedBy?.Name,
                    OperationId = operationId
                };

                dbContext.ArchiveOperations.Add(operationEntity);

                try
                {
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Group {GroupId} has been split into {NumberOfBatches} batches", groupId, operationEntity.NumberOfBatches);
                }
                catch (DbUpdateException ex) when (dbContext.IsDuplicateKeyException(ex))
                {
                    // Another handler beat us to it — load the existing operation
                    operationEntity = await dbContext.ArchiveOperations
                        .FindAsync(groupId, ArchiveType.FailureGroup, true);
                    logger.LogInformation("Archive operation for group {GroupId} already in progress, resuming at batch {CurrentBatch}/{NumberOfBatches}", groupId, operationEntity!.CurrentBatch, operationEntity.NumberOfBatches);
                }
            }

            // Capture audit attribution from the persisted entity
            auditUser = new AuditUser(operationEntity.InitiatedById ?? AuditUser.AnonymousValue, operationEntity.InitiatedByName ?? AuditUser.AnonymousValue);
            auditOperationId = operationEntity.OperationId;
        }

        // ── Start in-memory tracking ──
        await archivingManager.StartArchiving(operationEntity!);

        // ── Batch loop ──
        // Each iteration queries the first batchSize messages that still have Status = Unresolved.
        // Archiving them sets Status = Archived, so the next query naturally skips them — the
        // status change IS the cursor. No lastProcessedId needed. On resume after a crash, the
        // loop simply starts again; already-archived messages don't match the status filter.
        // The loop terminates when a query returns fewer than batchSize messages (the last
        // partial batch) or zero (nothing left).

        while (true)
        {
            using var batchScope = scopeFactory.CreateAsyncScope();
            var batchDbContext = batchScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            var batchIds = await ArchiveQueryHelper.GetNextBatchOfMessageIds(
                batchDbContext, groupId, FailedMessageStatus.Unresolved, batchSize);

            if (batchIds.Count == 0)
            {
                break; // No more unresolved messages in the group
            }

            logger.LogInformation("Archiving {MessageCount} messages from group {GroupId} starting", batchIds.Count, groupId);

            var now = DateTime.UtcNow;

            // Bulk status change with re-asserted status filter
            var affectedCount = await batchDbContext.FailedMessages
                .Where(fm => batchIds.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Unresolved)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Archived)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now));

            await archivingManager.BatchArchived(groupId, ArchiveType.FailureGroup, affectedCount);

            // Update persisted operation entity for progress tracking
            var persistedEntity = await batchDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, true);
            if (persistedEntity != null)
            {
                persistedEntity.CurrentBatch++;
                persistedEntity.NumberOfMessagesProcessed += affectedCount;
                await batchDbContext.SaveChangesAsync();
                operationEntity = persistedEntity;
            }

            // Raise batch domain event
            var messageIds = batchIds.Select(id => id.ToString()).ToArray();
            await domainEvents.Raise(new FailedMessageGroupBatchArchived
            {
                FailedMessagesIds = messageIds
            });

            // Per-message audit
            AuditArchivedMessages(MessageActionKind.Archive, Permissions.ErrorRecoverabilityGroupsArchive, auditUser, auditOperationId, messageIds);

            logger.LogInformation("Archiving of {MessageCount} messages from group {GroupId} completed", batchIds.Count, groupId);

            if (batchIds.Count < batchSize)
            {
                break; // Last partial batch — no more messages after these
            }
        }

        // ── Finalize ──
        logger.LogInformation("Archiving of group {GroupId} is complete", groupId);
        await archivingManager.ArchiveOperationFinalizing(groupId, ArchiveType.FailureGroup);

        // No wait-for-index step — SQL is immediately consistent

        await archivingManager.ArchiveOperationCompleted(groupId, ArchiveType.FailureGroup);

        // Delete the operation row
        using (var finalizeScope = scopeFactory.CreateAsyncScope())
        {
            var finalizeDbContext = finalizeScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var entity = await finalizeDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, true);
            if (entity != null)
            {
                finalizeDbContext.ArchiveOperations.Remove(entity);
                await finalizeDbContext.SaveChangesAsync();
            }
        }

        await domainEvents.Raise(new FailedMessageGroupArchived
        {
            GroupId = groupId,
            GroupName = operationEntity!.GroupName,
            MessagesCount = operationEntity!.TotalNumberOfMessages
        });

        logger.LogInformation("Archiving of group {GroupId} completed", groupId);
    }

    public async Task UnarchiveAllInGroup(string groupId, AuditUser? initiatedBy = null, string? operationId = null)
    {
        logger.LogInformation("Unarchiving of {GroupId} started", groupId);

        ArchiveOperationEntity? operationEntity;
        AuditUser auditUser;
        string? auditOperationId;

        // ── Load-or-create operation row ──
        using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            operationEntity = await dbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, false);

            if (operationEntity != null)
            {
                // Resume scenario
                logger.LogInformation("Resuming unarchive operation for group {GroupId} at batch {CurrentBatch}/{NumberOfBatches}", groupId, operationEntity.CurrentBatch, operationEntity.NumberOfBatches);
            }
            else
            {
                // New operation: get group details
                var (count, groupName) = await ArchiveQueryHelper.GetGroupDetails(dbContext, groupId, FailedMessageStatus.Archived, (CancellationToken)default);

                if (count == 0)
                {
                    logger.LogWarning("No messages to unarchive in group {GroupId}", groupId);
                    return;
                }

                operationEntity = new ArchiveOperationEntity
                {
                    RequestId = groupId,
                    GroupName = groupName,
                    ArchiveType = ArchiveType.FailureGroup,
                    IsArchive = false,
                    TotalNumberOfMessages = count,
                    NumberOfMessagesProcessed = 0,
                    NumberOfBatches = (int)Math.Ceiling(count / (float)batchSize),
                    CurrentBatch = 0,
                    Started = DateTime.UtcNow,
                    InitiatedById = initiatedBy?.Id,
                    InitiatedByName = initiatedBy?.Name,
                    OperationId = operationId
                };

                dbContext.ArchiveOperations.Add(operationEntity);

                try
                {
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Group {GroupId} has been split into {NumberOfBatches} batches", groupId, operationEntity.NumberOfBatches);
                }
                catch (DbUpdateException ex) when (dbContext.IsDuplicateKeyException(ex))
                {
                    // Another handler beat us to it — load the existing operation
                    operationEntity = await dbContext.ArchiveOperations
                        .FindAsync(groupId, ArchiveType.FailureGroup, false);
                    logger.LogInformation("Unarchive operation for group {GroupId} already in progress, resuming at batch {CurrentBatch}/{NumberOfBatches}", groupId, operationEntity!.CurrentBatch, operationEntity.NumberOfBatches);
                }
            }

            // Capture audit attribution from the persisted entity
            auditUser = new AuditUser(operationEntity.InitiatedById ?? AuditUser.AnonymousValue, operationEntity.InitiatedByName ?? AuditUser.AnonymousValue);
            auditOperationId = operationEntity.OperationId;
        }

        // ── Start in-memory tracking ──
        await unarchivingManager.StartUnarchiving(operationEntity!);

        // ── Batch loop ──
        // Each iteration queries the first batchSize messages that still have Status = Archived.
        // Unarchiving them sets Status = Unresolved, so the next query naturally skips them — the
        // status change IS the cursor. No lastProcessedId needed. On resume after a crash, the
        // loop simply starts again; already-unarchived messages don't match the status filter.
        // The loop terminates when a query returns fewer than batchSize messages (the last
        // partial batch) or zero (nothing left).

        while (true)
        {
            using var batchScope = scopeFactory.CreateAsyncScope();
            var batchDbContext = batchScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            var batchIds = await ArchiveQueryHelper.GetNextBatchOfMessageIds(
                batchDbContext, groupId, FailedMessageStatus.Archived, batchSize);

            if (batchIds.Count == 0)
            {
                break; // No more archived messages in the group
            }

            logger.LogInformation("Unarchiving {MessageCount} messages from group {GroupId} starting", batchIds.Count, groupId);

            var now = DateTime.UtcNow;

            // Bulk status change with re-asserted status filter
            var affectedCount = await batchDbContext.FailedMessages
                .Where(fm => batchIds.Contains(fm.UniqueMessageId) && fm.Status == FailedMessageStatus.Archived)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now));

            await unarchivingManager.BatchUnarchived(groupId, ArchiveType.FailureGroup, affectedCount);

            // Update persisted operation entity for progress tracking
            var persistedEntity = await batchDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, false);
            if (persistedEntity != null)
            {
                persistedEntity.CurrentBatch++;
                persistedEntity.NumberOfMessagesProcessed += affectedCount;
                await batchDbContext.SaveChangesAsync();
                operationEntity = persistedEntity;
            }

            // Raise batch domain event
            var messageIds = batchIds.Select(id => id.ToString()).ToArray();
            await domainEvents.Raise(new FailedMessageGroupBatchUnarchived
            {
                FailedMessagesIds = messageIds
            });

            // Per-message audit
            AuditArchivedMessages(MessageActionKind.Unarchive, Permissions.ErrorRecoverabilityGroupsUnarchive, auditUser, auditOperationId, messageIds);

            logger.LogInformation("Unarchiving of {MessageCount} messages from group {GroupId} completed", batchIds.Count, groupId);

            if (batchIds.Count < batchSize)
            {
                break; // Last partial batch — no more messages after these
            }
        }

        // ── Finalize ──
        logger.LogInformation("Unarchiving of group {GroupId} is complete", groupId);
        await unarchivingManager.UnarchiveOperationFinalizing(groupId, ArchiveType.FailureGroup);

        // No wait-for-index step — SQL is immediately consistent

        await unarchivingManager.UnarchiveOperationCompleted(groupId, ArchiveType.FailureGroup);

        // Delete the operation row
        using (var finalizeScope = scopeFactory.CreateAsyncScope())
        {
            var finalizeDbContext = finalizeScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var entity = await finalizeDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, false);
            if (entity != null)
            {
                finalizeDbContext.ArchiveOperations.Remove(entity);
                await finalizeDbContext.SaveChangesAsync();
            }
        }

        await domainEvents.Raise(new FailedMessageGroupUnarchived
        {
            GroupId = groupId,
            GroupName = operationEntity!.GroupName,
            MessagesCount = operationEntity!.TotalNumberOfMessages
        });

        logger.LogInformation("Unarchiving of group {GroupId} completed", groupId);
    }

    /// <summary>
    /// Emits one per-message audit entry for each message in a batch, correlated to the initiating
    /// operation. Skipped when no OperationId was captured (e.g. legacy in-flight operations).
    /// </summary>
    void AuditArchivedMessages(MessageActionKind kind, string permission, AuditUser user, string? operationId, string[] messageIds)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return;
        }

        foreach (var messageId in messageIds)
        {
            auditLog.MessageAction(user, kind, permission, MessageActionScope.Group, messageId, operationId);
        }
    }

    public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType)
        => operationsManager.IsOperationInProgressFor(groupId, archiveType);

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

    readonly IServiceScopeFactory scopeFactory;
    readonly OperationsManager operationsManager;
    readonly IDomainEvents domainEvents;
    readonly IMessageActionAuditLog auditLog;
    readonly EFCoreArchivingManager archivingManager;
    readonly EFCoreUnarchivingManager unarchivingManager;
    readonly ILogger<MessageArchiver> logger;
    const int batchSize = 1000;
}