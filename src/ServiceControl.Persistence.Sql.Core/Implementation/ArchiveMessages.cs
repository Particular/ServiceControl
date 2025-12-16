namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbContexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

public class ArchiveMessages : DataStoreBase, IArchiveMessages
{
    readonly IDomainEvents domainEvents;
    readonly ILogger<ArchiveMessages> logger;

    public ArchiveMessages(
        IServiceProvider serviceProvider,
        IDomainEvents domainEvents,
        ILogger<ArchiveMessages> logger) : base(serviceProvider)
    {
        this.domainEvents = domainEvents;
        this.logger = logger;
    }

    public async Task ArchiveAllInGroup(string groupId)
    {
        // This would update all failed messages in the group to archived status
        // For now, this is a placeholder that would need the failed message infrastructure
        logger.LogInformation("Archiving all messages in group {GroupId}", groupId);
        await Task.CompletedTask;
    }

    public async Task UnarchiveAllInGroup(string groupId)
    {
        logger.LogInformation("Unarchiving all messages in group {GroupId}", groupId);
        await Task.CompletedTask;
    }

    public bool IsOperationInProgressFor(string groupId, ArchiveType archiveType)
    {
        return ExecuteWithDbContext(dbContext =>
        {
            var operationId = MakeOperationId(groupId, archiveType);
            var operation = dbContext.ArchiveOperations
                .AsNoTracking()
                .FirstOrDefault(a => a.Id == Guid.Parse(operationId));

            if (operation == null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(operation.ArchiveState != (int)ArchiveState.ArchiveCompleted);
        }).Result;
    }

    public bool IsArchiveInProgressFor(string groupId)
    {
        return IsOperationInProgressFor(groupId, ArchiveType.FailureGroup) ||
               IsOperationInProgressFor(groupId, ArchiveType.All);
    }

    public void DismissArchiveOperation(string groupId, ArchiveType archiveType)
    {
        ExecuteWithDbContext(dbContext =>
        {
            var operationId = Guid.Parse(MakeOperationId(groupId, archiveType));

            dbContext.ArchiveOperations.Where(a => a.Id == operationId).ExecuteDelete();
            return Task.CompletedTask;
        }).Wait();
    }

    public Task StartArchiving(string groupId, ArchiveType archiveType)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var operation = new ArchiveOperationEntity
            {
                Id = Guid.Parse(MakeOperationId(groupId, archiveType)),
                RequestId = groupId,
                GroupName = groupId,
                ArchiveType = (int)archiveType,
                ArchiveState = (int)ArchiveState.ArchiveStarted,
                TotalNumberOfMessages = 0,
                NumberOfMessagesArchived = 0,
                NumberOfBatches = 0,
                CurrentBatch = 0,
                Started = DateTime.UtcNow
            };

            await dbContext.ArchiveOperations.AddAsync(operation);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Started archiving for group {GroupId}", groupId);
        });
    }

    public Task StartUnarchiving(string groupId, ArchiveType archiveType)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var operation = new ArchiveOperationEntity
            {
                Id = Guid.Parse(MakeOperationId(groupId, archiveType)),
                RequestId = groupId,
                GroupName = groupId,
                ArchiveType = (int)archiveType,
                ArchiveState = (int)ArchiveState.ArchiveStarted,
                TotalNumberOfMessages = 0,
                NumberOfMessagesArchived = 0,
                NumberOfBatches = 0,
                CurrentBatch = 0,
                Started = DateTime.UtcNow
            };

            await dbContext.ArchiveOperations.AddAsync(operation);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Started unarchiving for group {GroupId}", groupId);
        });
    }

    public IEnumerable<InMemoryArchive> GetArchivalOperations()
    {
        // Note: IEnumerable methods need direct scope management as they yield results
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

        var operations = dbContext.ArchiveOperations
            .AsNoTracking()
            .AsEnumerable();

        foreach (var op in operations)
        {
            yield return new InMemoryArchive(op.RequestId, (ArchiveType)op.ArchiveType, domainEvents)
            {
                GroupName = op.GroupName,
                ArchiveState = (ArchiveState)op.ArchiveState,
                TotalNumberOfMessages = op.TotalNumberOfMessages,
                NumberOfMessagesArchived = op.NumberOfMessagesArchived,
                NumberOfBatches = op.NumberOfBatches,
                CurrentBatch = op.CurrentBatch,
                Started = op.Started,
                Last = op.Last,
                CompletionTime = op.CompletionTime
            };
        }
    }

    static string MakeOperationId(string groupId, ArchiveType archiveType)
    {
        return $"{archiveType}/{groupId}";
    }
}
