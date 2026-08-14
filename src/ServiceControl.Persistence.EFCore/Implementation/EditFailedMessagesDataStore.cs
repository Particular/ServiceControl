namespace ServiceControl.Persistence.EFCore.Implementation;

using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.MessageFailures;

public class EditFailedMessagesDataStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    : DataStoreBase(scopeFactory), IEditFailedMessagesDataStore
{
    public Task<string?> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
        {
            return Task.FromResult<string?>(null);
        }

        return ExecuteWithDbContext((dbContext, token) => dbContext.FailedMessageEdits
            .AsNoTracking()
            .Where(edit => edit.UniqueMessageId == uniqueMessageId)
            .Select(edit => edit.EditId)
            .SingleOrDefaultAsync(token), cancellationToken);
    }

    public Task<BeginEditResult> TryBeginEdit(string failedMessageId, string editingMessageId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(failedMessageId, out var uniqueMessageId))
        {
            return Task.FromResult(new BeginEditResult(BeginEditOutcome.MessageNotFound));
        }

        return ExecuteWithDbContext(async (dbContext, ct) =>
            {
                var entity = await dbContext.FailedMessages
                    .SingleOrDefaultAsync(message => message.UniqueMessageId == uniqueMessageId, ct);

                if (entity is null)
                {
                    return new BeginEditResult(BeginEditOutcome.MessageNotFound);
                }

                var existingEditId = await dbContext.FailedMessageEdits
                    .Where(edit => edit.UniqueMessageId == uniqueMessageId)
                    .Select(edit => edit.EditId)
                    .SingleOrDefaultAsync(ct);

                if (existingEditId is not null)
                {
                    return existingEditId == editingMessageId
                        ? new BeginEditResult(BeginEditOutcome.Acquired, entity.ToFailedMessage([]), existingEditId)
                        : new BeginEditResult(BeginEditOutcome.AcquiredByAnotherEdit, ExistingEditId: existingEditId);
                }

                if (entity.Status != FailedMessageStatus.Unresolved)
                {
                    return new BeginEditResult(BeginEditOutcome.MessageNotUnresolved);
                }

                dbContext.FailedMessageEdits.Add(new FailedMessageEditEntity { UniqueMessageId = uniqueMessageId, EditId = editingMessageId });

                var now = timeProvider.GetUtcNow().UtcDateTime;
                entity.Status = FailedMessageStatus.Resolved;
                entity.StatusChangedAt = now;
                entity.LastModified = now;

                try
                {
                    await dbContext.SaveChangesAsync(ct);
                    return new BeginEditResult(BeginEditOutcome.Acquired, entity.ToFailedMessage([]));
                }
                catch (DbUpdateException exception) when (dbContext.IsDuplicateKeyException(exception))
                {
                    dbContext.ChangeTracker.Clear();

                    var winningEditId = await dbContext.FailedMessageEdits
                        .AsNoTracking()
                        .Where(edit => edit.UniqueMessageId == uniqueMessageId)
                        .Select(edit => edit.EditId)
                        .SingleOrDefaultAsync(ct);

                    if (winningEditId is null)
                    {
                        throw;
                    }

                    if (winningEditId != editingMessageId)
                    {
                        return new BeginEditResult(BeginEditOutcome.AcquiredByAnotherEdit, ExistingEditId: winningEditId);
                    }

                    entity = await dbContext.FailedMessages
                        .AsNoTracking()
                        .SingleAsync(message => message.UniqueMessageId == uniqueMessageId, ct);

                    return new BeginEditResult(BeginEditOutcome.Acquired, entity.ToFailedMessage([]), winningEditId);
                }
            }, cancellationToken);
    }
}