namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

public class EditFailedMessagesManager(IAsyncDisposable scope, ServiceControlDbContext dbContext, TimeProvider timeProvider) : IEditFailedMessagesManager
{
    FailedMessage? failedMessage;            // cached after GetFailedMessage
    FailedMessageEditEntity? editEntity;     // tracked after GetCurrentEditingRequestId / SetCurrentEditingRequestId

    public async Task<FailedMessage?> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
        {
            return null;
        }

        var entity = await dbContext.FailedMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.UniqueMessageId == uniqueMessageId, cancellationToken);

        if (entity == null)
        {
            return null!;
        }

        // Reuse the same mapping helper as FailedMessageQueryDataStore so the manager and the
        // query store don't diverge. The edit manager passes an empty group list (it does not
        // need failure groups); ToFailedMessage tolerates an empty collection.
        var result = entity.ToFailedMessage([]);
        failedMessage = result;
        return result;
    }

    public async Task<string?> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
        {
            return null!;
        }

        editEntity = await dbContext.FailedMessageEdits
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.UniqueMessageId == uniqueMessageId, cancellationToken);

        return editEntity?.EditId;
    }

    public Task SetCurrentEditingRequestId(string editingMessageId, CancellationToken cancellationToken = default)
    {
        if (failedMessage == null)
        {
            throw new InvalidOperationException("No failed message loaded");
        }

        editEntity = new FailedMessageEditEntity
        {
            UniqueMessageId = Guid.Parse(failedMessage.UniqueMessageId),
            EditId = editingMessageId
        };
        dbContext.FailedMessageEdits.Add(editEntity);

        return Task.CompletedTask;
    }

    public async Task SetFailedMessageAsResolved(CancellationToken cancellationToken = default)
    {
        if (failedMessage == null)
        {
            throw new InvalidOperationException("No failed message loaded");
        }

        var message = failedMessage;

        // Critical: must update the tracked entity (not just the in-memory FailedMessage) and
        // MUST set StatusChangedAt + LastModified so the retention sweeper's filter
        // (StatusChangedAt < cutoff AND Status in Resolved/Archived) works correctly. Leaving
        // StatusChangedAt at its previous value could cause the just-resolved message to be
        // swept immediately.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = await dbContext.FailedMessages
            .SingleOrDefaultAsync(m => m.UniqueMessageId == Guid.Parse(message.UniqueMessageId), cancellationToken)
            ?? throw new InvalidOperationException("Failed message entity not found");

        entity.Status = FailedMessageStatus.Resolved;
        entity.StatusChangedAt = now;
        entity.LastModified = now;
        message.Status = FailedMessageStatus.Resolved;
    }

    public Task SaveChanges(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}