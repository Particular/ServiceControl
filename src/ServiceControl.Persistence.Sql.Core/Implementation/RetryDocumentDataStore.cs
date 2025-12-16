namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

public class RetryDocumentDataStore : DataStoreBase, IRetryDocumentDataStore
{
    readonly ILogger<RetryDocumentDataStore> logger;

    public RetryDocumentDataStore(
        IServiceProvider serviceProvider,
        ILogger<RetryDocumentDataStore> logger) : base(serviceProvider)
    {
        this.logger = logger;
    }

    public Task StageRetryByUniqueMessageIds(string batchDocumentId, string[] messageIds)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            foreach (var messageId in messageIds)
            {
                var retryId = FailedMessageRetry.MakeDocumentId(messageId);
                var existing = await dbContext.FailedMessageRetries.FindAsync(Guid.Parse(retryId));

                if (existing == null)
                {
                    // Create new retry document
                    var newRetry = new FailedMessageRetryEntity
                    {
                        Id = Guid.Parse(retryId),
                        FailedMessageId = $"FailedMessages/{messageId}",
                        RetryBatchId = batchDocumentId,
                        StageAttempts = 0
                    };
                    await dbContext.FailedMessageRetries.AddAsync(newRetry);
                }
                else
                {
                    // Update existing retry document
                    existing.FailedMessageId = $"FailedMessages/{messageId}";
                    existing.RetryBatchId = batchDocumentId;
                }
            }

            await dbContext.SaveChangesAsync();
        });
    }

    public Task MoveBatchToStaging(string batchDocumentId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            try
            {
                var batch = await dbContext.RetryBatches.FirstOrDefaultAsync(b => b.Id == Guid.Parse(batchDocumentId));
                if (batch != null)
                {
                    batch.Status = RetryBatchStatus.Staging;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                logger.LogDebug("Ignoring concurrency exception while moving batch to staging {BatchDocumentId}", batchDocumentId);
            }
        });
    }

    public Task<string> CreateBatchDocument(
        string retrySessionId,
        string requestId,
        RetryType retryType,
        string[] failedMessageRetryIds,
        string originator,
        DateTime startTime,
        DateTime? last = null,
        string? batchName = null,
        string? classifier = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var batchDocumentId = RetryBatch.MakeDocumentId(Guid.NewGuid().ToString());

            var batch = new RetryBatchEntity
            {
                Id = Guid.Parse(batchDocumentId),
                Context = batchName,
                RequestId = requestId,
                RetryType = retryType,
                Originator = originator,
                Classifier = classifier,
                StartTime = startTime,
                Last = last,
                InitialBatchSize = failedMessageRetryIds.Length,
                RetrySessionId = retrySessionId,
                FailureRetriesJson = JsonSerializer.Serialize(failedMessageRetryIds, JsonSerializationOptions.Default),
                Status = RetryBatchStatus.MarkingDocuments
            };

            await dbContext.RetryBatches.AddAsync(batch);
            await dbContext.SaveChangesAsync();

            return batchDocumentId;
        });
    }

    public Task<QueryResult<IList<RetryBatch>>> QueryOrphanedBatches(string retrySessionId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var orphanedBatches = await dbContext.RetryBatches
                .Where(b => b.Status == RetryBatchStatus.MarkingDocuments && b.RetrySessionId != retrySessionId)
                .AsNoTracking()
                .ToListAsync();

            var result = orphanedBatches.Select(entity => new RetryBatch
            {
                Id = entity.Id.ToString(),
                Context = entity.Context,
                RetrySessionId = entity.RetrySessionId,
                RequestId = entity.RequestId,
                StagingId = entity.StagingId,
                Originator = entity.Originator,
                Classifier = entity.Classifier,
                StartTime = entity.StartTime,
                Last = entity.Last,
                InitialBatchSize = entity.InitialBatchSize,
                Status = entity.Status,
                RetryType = entity.RetryType,
                FailureRetries = JsonSerializer.Deserialize<List<string>>(entity.FailureRetriesJson, JsonSerializationOptions.Default) ?? []
            }).ToList();

            return new QueryResult<IList<RetryBatch>>(result, new QueryStatsInfo(string.Empty, result.Count, false));
        });
    }

    public Task<IList<RetryBatchGroup>> QueryAvailableBatches()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query all batches that are either Staging or Forwarding
            var results = await dbContext.RetryBatches
                .AsNoTracking()
                .Where(b => b.Status == RetryBatchStatus.Staging || b.Status == RetryBatchStatus.Forwarding)
                .GroupBy(b => new { b.RequestId, b.RetryType, b.Originator, b.Classifier })
                .Select(g => new RetryBatchGroup
                {
                    RequestId = g.Key.RequestId,
                    RetryType = g.Key.RetryType,
                    Originator = g.Key.Originator,
                    Classifier = g.Key.Classifier,
                    HasStagingBatches = g.Any(b => b.Status == RetryBatchStatus.Staging),
                    HasForwardingBatches = g.Any(b => b.Status == RetryBatchStatus.Forwarding),
                    InitialBatchSize = g.Sum(b => b.InitialBatchSize),
                    StartTime = g.Min(b => b.StartTime),
                    Last = g.Max(b => b.Last) ?? g.Max(b => b.StartTime)
                })
                .ToListAsync();

            return (IList<RetryBatchGroup>)results;
        });
    }

    public Task GetBatchesForAll(DateTime cutoff, Func<string, DateTime, Task> callback)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var messages = dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    m.UniqueMessageId,
                    m.LastProcessedAt
                })
                .AsAsyncEnumerable();

            await foreach (var message in messages)
            {
                var timeOfFailure = message.LastProcessedAt ?? DateTime.UtcNow;
                await callback(message.UniqueMessageId, timeOfFailure);
            }
        });
    }

    public Task GetBatchesForEndpoint(DateTime cutoff, string endpoint, Func<string, DateTime, Task> callback)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var messages = dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved && m.ReceivingEndpointName == endpoint)
                .Select(m => new
                {
                    m.UniqueMessageId,
                    m.LastProcessedAt
                })
                .AsAsyncEnumerable();

            await foreach (var message in messages)
            {
                var timeOfFailure = message.LastProcessedAt ?? DateTime.UtcNow;
                await callback(message.UniqueMessageId, timeOfFailure);
            }
        });
    }

    public Task GetBatchesForFailedQueueAddress(DateTime cutoff, string failedQueueAddress, FailedMessageStatus status, Func<string, DateTime, Task> callback)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var messages = dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved && m.QueueAddress == failedQueueAddress && m.Status == status)
                .Select(m => new
                {
                    m.UniqueMessageId,
                    m.LastProcessedAt
                })
                .AsAsyncEnumerable();

            await foreach (var message in messages)
            {
                var timeOfFailure = message.LastProcessedAt ?? DateTime.UtcNow;
                await callback(message.UniqueMessageId, timeOfFailure);
            }
        });
    }

    public Task GetBatchesForFailureGroup(string groupId, string groupTitle, string groupType, DateTime cutoff, Func<string, DateTime, Task> callback)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query all unresolved messages and filter by group in memory (since groups are in JSON)
            var messages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    m.UniqueMessageId,
                    m.LastProcessedAt,
                    m.FailureGroupsJson
                })
                .ToListAsync();

            foreach (var message in messages)
            {
                var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(message.FailureGroupsJson, JsonSerializationOptions.Default) ?? [];
                if (groups.Any(g => g.Id == groupId))
                {
                    var timeOfFailure = message.LastProcessedAt ?? DateTime.UtcNow;
                    await callback(message.UniqueMessageId, timeOfFailure);
                }
            }
        });
    }

    public Task<FailureGroupView?> QueryFailureGroupViewOnGroupId(string groupId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query all unresolved messages and find those with this group
            var messages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    m.FailureGroupsJson,
                    m.LastProcessedAt
                })
                .ToListAsync();

            FailedMessage.FailureGroup? matchingGroup = null;
            var matchingMessages = new List<DateTime?>();

            foreach (var message in messages)
            {
                var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(message.FailureGroupsJson, JsonSerializationOptions.Default) ?? [];
                var group = groups.FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    matchingGroup ??= group;
                    matchingMessages.Add(message.LastProcessedAt);
                }
            }

            if (matchingGroup == null || matchingMessages.Count == 0)
            {
                return null;
            }

            // Load comment
            var comment = await dbContext.GroupComments
                .Where(c => c.GroupId == groupId)
                .Select(c => c.Comment)
                .FirstOrDefaultAsync();

            return new FailureGroupView
            {
                Id = matchingGroup.Id,
                Title = matchingGroup.Title,
                Type = matchingGroup.Type,
                Count = matchingMessages.Count,
                First = matchingMessages.Min() ?? DateTime.UtcNow,
                Last = matchingMessages.Max() ?? DateTime.UtcNow,
                Comment = comment
            };
        });
    }
}
