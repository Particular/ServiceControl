namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;

public class GroupsDataStore : DataStoreBase, IGroupsDataStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GroupsDataStore(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier, string classifierFilter)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query failed messages with unresolved status to build failure group views
            var failedMessages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(m => m.Status == FailedMessageStatus.Unresolved)
                .Select(m => new
                {
                    m.FailureGroupsJson,
                    m.LastProcessedAt
                })
                .ToListAsync();

            // Deserialize and flatten failure groups
            var allGroups = failedMessages
                .SelectMany(m =>
                {
                    var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(m.FailureGroupsJson, JsonOptions) ?? [];
                    return groups.Select(g => new { Group = g, ProcessedAt = m.LastProcessedAt });
                })
                .Where(x => x.Group.Type == classifier)
                .ToList();

            // Apply classifier filter if specified
            if (!string.IsNullOrWhiteSpace(classifierFilter))
            {
                allGroups = allGroups.Where(x => x.Group.Title == classifierFilter).ToList();
            }

            // Group and aggregate
            var groupViews = allGroups
                .GroupBy(x => x.Group.Id)
                .Select(g => new
                {
                    g.First().Group,
                    Count = g.Count(),
                    First = g.Min(x => x.ProcessedAt) ?? DateTime.UtcNow,
                    Last = g.Max(x => x.ProcessedAt) ?? DateTime.UtcNow
                })
                .OrderByDescending(x => x.Last)
                .Take(200)
                .ToList();

            // Load comments for these groups
            var groupIds = groupViews.Select(g => g.Group.Id).ToList();
            var commentLookup = await dbContext.GroupComments
                .AsNoTracking()
                .Where(c => groupIds.Contains(c.GroupId))
                .ToDictionaryAsync(c => c.GroupId, c => c.Comment);

            // Build result
            var result = groupViews.Select(g => new FailureGroupView
            {
                Id = g.Group.Id,
                Title = g.Group.Title,
                Type = g.Group.Type,
                Count = g.Count,
                First = g.First,
                Last = g.Last,
                Comment = commentLookup.GetValueOrDefault(g.Group.Id)
            }).ToList();

            return (IList<FailureGroupView>)result;
        });
    }

    public Task<RetryBatch?> GetCurrentForwardingBatch()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var nowForwarding = await dbContext.RetryBatchNowForwarding
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == RetryBatchNowForwardingEntity.SingletonId);

            if (nowForwarding == null || string.IsNullOrEmpty(nowForwarding.RetryBatchId))
            {
                return null;
            }

            var batchEntity = await dbContext.RetryBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == Guid.Parse(nowForwarding.RetryBatchId));

            if (batchEntity == null)
            {
                return null;
            }

            return new RetryBatch
            {
                Id = batchEntity.Id.ToString(),
                Context = batchEntity.Context,
                RetrySessionId = batchEntity.RetrySessionId,
                RequestId = batchEntity.RequestId,
                StagingId = batchEntity.StagingId,
                Originator = batchEntity.Originator,
                Classifier = batchEntity.Classifier,
                StartTime = batchEntity.StartTime,
                Last = batchEntity.Last,
                InitialBatchSize = batchEntity.InitialBatchSize,
                Status = batchEntity.Status,
                RetryType = batchEntity.RetryType,
                FailureRetries = JsonSerializer.Deserialize<List<string>>(batchEntity.FailureRetriesJson, JsonOptions) ?? []
            };
        });
    }
}
