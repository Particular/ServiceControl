namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;

partial class ErrorMessageDataStore
{
    public Task FailedMessageMarkAsArchived(string failedMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var failedMessage = await dbContext.FailedMessages
                .FirstOrDefaultAsync(fm => fm.Id == Guid.Parse(failedMessageId));

            if (failedMessage != null)
            {
                failedMessage.Status = FailedMessageStatus.Archived;
                await dbContext.SaveChangesAsync();
            }
        });
    }

    public Task<bool> MarkMessageAsResolved(string failedMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var failedMessage = await dbContext.FailedMessages
                .FirstOrDefaultAsync(fm => fm.Id == Guid.Parse(failedMessageId));

            if (failedMessage == null)
            {
                return false;
            }

            failedMessage.Status = FailedMessageStatus.Resolved;
            await dbContext.SaveChangesAsync();
            return true;
        });
    }

    public Task ProcessPendingRetries(DateTime periodFrom, DateTime periodTo, string queueAddress, Func<string, Task> processCallback)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.Status == FailedMessageStatus.RetryIssued &&
                             fm.LastProcessedAt >= periodFrom &&
                             fm.LastProcessedAt < periodTo);

            if (!string.IsNullOrWhiteSpace(queueAddress))
            {
                query = query.Where(fm => fm.QueueAddress == queueAddress);
            }

            var failedMessageIds = await query
                .Select(fm => fm.Id)
                .ToListAsync();

            foreach (var failedMessageId in failedMessageIds)
            {
                await processCallback(failedMessageId.ToString());
            }
        });
    }

    public Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // First, get the unique message IDs that will be affected
            var uniqueMessageIds = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.Status == FailedMessageStatus.Archived &&
                             fm.LastProcessedAt >= from &&
                             fm.LastProcessedAt < to)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync();

            // Then update all matching messages in a single operation
            await dbContext.FailedMessages
                .Where(fm => fm.Status == FailedMessageStatus.Archived &&
                             fm.LastProcessedAt >= from &&
                             fm.LastProcessedAt < to)
                .ExecuteUpdateAsync(setters => setters.SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved));

            return uniqueMessageIds.ToArray();
        });
    }

    public Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Convert string IDs to Guids for querying
            var messageGuids = failedMessageIds.Select(Guid.Parse).ToList();

            // First, get the unique message IDs that will be affected
            var uniqueMessageIds = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => messageGuids.Contains(fm.Id) && fm.Status == FailedMessageStatus.Archived)
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync();

            // Then update all matching messages in a single operation
            await dbContext.FailedMessages
                .Where(fm => messageGuids.Contains(fm.Id) && fm.Status == FailedMessageStatus.Archived)
                .ExecuteUpdateAsync(setters => setters.SetProperty(fm => fm.Status, FailedMessageStatus.Unresolved));

            return uniqueMessageIds.ToArray();
        });
    }

    public Task RevertRetry(string messageUniqueId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Change status back to Unresolved
            var failedMessage = await dbContext.FailedMessages
                .FirstOrDefaultAsync(fm => fm.UniqueMessageId == messageUniqueId);

            if (failedMessage != null)
            {
                failedMessage.Status = FailedMessageStatus.Unresolved;
                await dbContext.SaveChangesAsync();
            }
        });
    }

    public Task RemoveFailedMessageRetryDocument(string uniqueMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var retryDocumentId = $"FailedMessages/{uniqueMessageId}";
            var retryDocument = await dbContext.FailedMessageRetries
                .FirstOrDefaultAsync(r => r.FailedMessageId == retryDocumentId);

            if (retryDocument != null)
            {
                dbContext.FailedMessageRetries.Remove(retryDocument);
                await dbContext.SaveChangesAsync();
            }
        });
    }

    public Task<string[]> GetRetryPendingMessages(DateTime from, DateTime to, string queueAddress)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.Status == FailedMessageStatus.RetryIssued &&
                             fm.LastProcessedAt >= from &&
                             fm.LastProcessedAt < to);

            if (!string.IsNullOrWhiteSpace(queueAddress))
            {
                query = query.Where(fm => fm.QueueAddress == queueAddress);
            }

            var messageIds = await query
                .Select(fm => fm.UniqueMessageId)
                .ToListAsync();

            return messageIds.ToArray();
        });
    }
}
