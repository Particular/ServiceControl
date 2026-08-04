namespace ServiceControl.Persistence.EFCore.Implementation.Recoverability;

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

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            operationEntity = await GetOrCreateOperation(dbContext, groupId, ArchiveOperationType.UnArchive, initiatedBy, operationId);
            if (operationEntity == null)
            {
                return;
            }

            // Capture audit attribution from the persisted entity
            auditUser = new AuditUser(operationEntity.InitiatedById ?? AuditUser.AnonymousValue, operationEntity.InitiatedByName ?? AuditUser.AnonymousValue);
            auditOperationId = operationEntity.OperationId;
        }

        // ── Start in-memory tracking ──
        await archivingManager.StartArchiving(operationEntity);

        // ── Batch loop ──
        string[] batchIds;
        do
        {
            logger.LogInformation("Archiving messages from group {GroupId} starting", groupId);

            await using var batchScope = scopeFactory.CreateAsyncScope();
            var batchDbContext = batchScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            batchIds = await UpdateGroupStatusAsync(batchDbContext, groupId, FailedMessageStatus.Unresolved, FailedMessageStatus.Archived, batchSize);
            await archivingManager.BatchArchived(groupId, ArchiveType.FailureGroup, batchIds.Length);

            // Update persisted operation entity for progress tracking
            var persistedEntity = await batchDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, true);
            if (persistedEntity != null)
            {
                persistedEntity.CurrentBatch++;
                persistedEntity.NumberOfMessagesProcessed += batchIds.Length;
                await batchDbContext.SaveChangesAsync();
                operationEntity = persistedEntity;
            }

            // Raise batch domain event
            await domainEvents.Raise(new FailedMessageGroupBatchArchived { FailedMessagesIds = batchIds });

            // Per-message audit
            AuditArchivedMessages(MessageActionKind.Archive, Permissions.ErrorRecoverabilityGroupsArchive, auditUser, auditOperationId, batchIds);

            logger.LogInformation("Archiving of {MessageCount} messages from group {GroupId} completed", batchIds.Length, groupId);
        } while (batchIds.Length >= batchSize);

        // ── Finalize ──
        logger.LogInformation("Archiving of group {GroupId} is complete", groupId);
        await archivingManager.ArchiveOperationFinalizing(groupId, ArchiveType.FailureGroup);
        await archivingManager.ArchiveOperationCompleted(groupId, ArchiveType.FailureGroup);

        // Delete the operation row
        await using (var finalizeScope = scopeFactory.CreateAsyncScope())
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
            GroupName = operationEntity.GroupName,
            MessagesCount = operationEntity.TotalNumberOfMessages
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

            operationEntity = await GetOrCreateOperation(dbContext, groupId, ArchiveOperationType.UnArchive, initiatedBy, operationId);
            if (operationEntity == null)
            {
                return;
            }

            // Capture audit attribution from the persisted entity
            auditUser = new AuditUser(operationEntity.InitiatedById ?? AuditUser.AnonymousValue, operationEntity.InitiatedByName ?? AuditUser.AnonymousValue);
            auditOperationId = operationEntity.OperationId;
        }

        await unarchivingManager.StartUnarchiving(operationEntity!);
        string[] batchIds;
        do
        {
            await using var batchScope = scopeFactory.CreateAsyncScope();
            var batchDbContext = batchScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            logger.LogInformation("Unarchiving messages from group {GroupId} starting", groupId);
            batchIds = await UpdateGroupStatusAsync(batchDbContext, groupId, FailedMessageStatus.Archived, FailedMessageStatus.Unresolved, batchSize);

            await unarchivingManager.BatchUnarchived(groupId, ArchiveType.FailureGroup, batchIds.Length);

            // Update persisted operation entity for progress tracking
            var persistedEntity = await batchDbContext.ArchiveOperations
                .FindAsync(groupId, ArchiveType.FailureGroup, false);
            if (persistedEntity != null)
            {
                persistedEntity.CurrentBatch++;
                persistedEntity.NumberOfMessagesProcessed += batchIds.Length;
                await batchDbContext.SaveChangesAsync();
                operationEntity = persistedEntity;
            }

            // Raise batch domain event
            await domainEvents.Raise(new FailedMessageGroupBatchUnarchived { FailedMessagesIds = batchIds });

            // Per-message audit
            AuditArchivedMessages(MessageActionKind.Unarchive, Permissions.ErrorRecoverabilityGroupsUnarchive, auditUser, auditOperationId, batchIds);

            logger.LogInformation("Unarchiving of {MessageCount} messages from group {GroupId} completed", batchIds.Length, groupId);
        } while (batchIds.Length >= batchSize);

        // ── Finalize ──
        logger.LogInformation("Unarchiving of group {GroupId} is complete", groupId);
        await unarchivingManager.UnarchiveOperationFinalizing(groupId, ArchiveType.FailureGroup);
        await unarchivingManager.UnarchiveOperationCompleted(groupId, ArchiveType.FailureGroup);

        // Delete the operation row
        await using (var finalizeScope = scopeFactory.CreateAsyncScope())
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

    async Task<ArchiveOperationEntity?> GetOrCreateOperation(ServiceControlDbContext dbContext, string groupId, ArchiveOperationType operation, AuditUser? initiatedBy, string? operationId)
    {
        ArchiveOperationEntity? operationEntity = await dbContext.ArchiveOperations.FindAsync(groupId, ArchiveType.FailureGroup, false);

        if (operationEntity != null)
        {
            logger.LogInformation("Resuming {OperationType} operation for group {GroupId} at batch {CurrentBatch}/{NumberOfBatches}", operation.ToString(), groupId, operationEntity.CurrentBatch, operationEntity.NumberOfBatches);
        }
        else
        {
            var (count, groupName) = await GetGroupDetails(dbContext, groupId, FailedMessageStatus.Archived);
            if (count == 0)
            {
                logger.LogWarning("No messages to {OperationType} in group {GroupId}", operation.ToString(), groupId);
                return operationEntity;
            }

            operationEntity = new ArchiveOperationEntity
            {
                RequestId = groupId,
                GroupName = groupName,
                ArchiveType = ArchiveType.FailureGroup,
                OperationType = operation,
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
                //Concurrency issue and process has already started it, nothing to do until restart.
                return null;
            }
        }

        return operationEntity;
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

    async Task<string[]> UpdateGroupStatusAsync(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus fromStatus, FailedMessageStatus toStatus, int batchSize, CancellationToken cancellationToken = default)
    {
        var batchIds = await GetNextBatch(dbContext, groupId, fromStatus, batchSize)
            .Select(x => x.UniqueMessageId)
            .ToListAsync(cancellationToken);

        if (batchIds.Count > 0)
        {
            var now = DateTime.UtcNow;

            // Bulk status change with re-asserted status filter
            await dbContext.FailedMessages
                .Where(fm => batchIds.Contains(fm.UniqueMessageId) && fm.Status == fromStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(fm => fm.Status, toStatus)
                    .SetProperty(fm => fm.StatusChangedAt, now)
                    .SetProperty(fm => fm.LastModified, now), cancellationToken);
        }

        return batchIds.Select(id => id.ToString()).ToArray();
    }

    static async Task<(int count, string groupName)> GetGroupDetails(
        ServiceControlDbContext dbContext, string groupId, FailedMessageStatus status, CancellationToken cancellationToken = default)
    {
        var query =
            from fmg in dbContext.FailedMessageGroups
            join fm in dbContext.FailedMessages
                on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
            where fmg.GroupId == groupId && fm.Status == status
            select new { fmg.Title, fm.UniqueMessageId };

        var count = await query.CountAsync(cancellationToken);
        var groupName = await dbContext.FailedMessageGroups
            .Where(fmg => fmg.GroupId == groupId)
            .Select(fmg => fmg.Title)
            .FirstOrDefaultAsync(cancellationToken) ?? "Undefined";

        return (count, groupName);
    }

    static IQueryable<FailedMessageEntity> GetGroup(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus status) =>
        from fmg in dbContext.FailedMessageGroups
        join fm in dbContext.FailedMessages on fmg.FailedMessageUniqueId equals fm.UniqueMessageId
        where fmg.GroupId == groupId && fm.Status == status
        orderby fm.UniqueMessageId
        select fm;

    static IQueryable<FailedMessageEntity> GetNextBatch(ServiceControlDbContext dbContext, string groupId, FailedMessageStatus status, int batchSize) =>
        GetGroup(dbContext, groupId, status).Take(batchSize);

    readonly IServiceScopeFactory scopeFactory;
    readonly OperationsManager operationsManager;
    readonly IDomainEvents domainEvents;
    readonly IMessageActionAuditLog auditLog;
    readonly EFCoreArchivingManager archivingManager;
    readonly EFCoreUnarchivingManager unarchivingManager;
    readonly ILogger<MessageArchiver> logger;
    const int batchSize = 1000;
}