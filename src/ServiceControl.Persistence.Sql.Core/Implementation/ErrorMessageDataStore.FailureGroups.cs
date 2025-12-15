namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Entities;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Recoverability;

partial class ErrorMessageDataStore
{
    public Task<QueryResult<FailureGroupView>> GetFailureGroupView(string groupId, string status, string modified)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query failed messages filtered by PrimaryFailureGroupId at database level
            var messages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.PrimaryFailureGroupId == groupId)
                .ToListAsync();

            // Deserialize failure groups to get the primary group details
            var allGroups = messages
                .Select(fm =>
                {
                    var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(fm.FailureGroupsJson) ?? [];
                    // Take the first group (which matches PrimaryFailureGroupId == groupId)
                    var primaryGroup = groups.FirstOrDefault();
                    return new
                    {
                        Group = primaryGroup,
                        MessageId = fm.Id,
                        LastProcessedAt = fm.LastProcessedAt ?? DateTime.MinValue
                    };
                })
                .Where(x => x.Group != null)
                .ToList();

            if (!allGroups.Any())
            {
                return new QueryResult<FailureGroupView>(null!, new QueryStatsInfo("0", 0, false));
            }

            // Aggregate the group data
            var firstGroup = allGroups.First().Group!; // Safe: allGroups is filtered to non-null Groups

            // Retrieve comment if exists
            var commentEntity = await dbContext.GroupComments
                .AsNoTracking()
                .FirstOrDefaultAsync(gc => gc.GroupId == groupId);

            var view = new FailureGroupView
            {
                Id = groupId,
                Title = firstGroup.Title,
                Type = firstGroup.Type,
                Count = allGroups.Count,
                Comment = commentEntity?.Comment ?? string.Empty,
                First = allGroups.Min(x => x.LastProcessedAt),
                Last = allGroups.Max(x => x.LastProcessedAt)
            };

            return new QueryResult<FailureGroupView>(view, new QueryStatsInfo("1", 1, false));
        });
    }

    public Task<IList<FailureGroupView>> GetFailureGroupsByClassifier(string classifier)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Query all failed messages - optimize by selecting only required columns
            // Note: Cannot filter by PrimaryFailureGroupId since we're filtering by classifier (Type)
            var messages = await dbContext.FailedMessages
                .AsNoTracking()
                .Select(fm => new { fm.FailureGroupsJson, fm.LastProcessedAt })
                .ToListAsync();

            // Deserialize and group by failure group ID
            var groupedData = messages
                .SelectMany(fm =>
                {
                    var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(fm.FailureGroupsJson) ?? [];
                    return groups.Select(g => new
                    {
                        Group = g,
                        LastProcessedAt = fm.LastProcessedAt ?? DateTime.MinValue
                    });
                })
                .Where(x => x.Group.Type == classifier)
                .GroupBy(x => x.Group.Id)
                .Select(g => new FailureGroupView
                {
                    Id = g.Key,
                    Title = g.First().Group.Title,
                    Type = g.First().Group.Type,
                    Count = g.Count(),
                    Comment = string.Empty,
                    First = g.Min(x => x.LastProcessedAt),
                    Last = g.Max(x => x.LastProcessedAt)
                })
                .OrderByDescending(g => g.Last)
                .ToList();

            return (IList<FailureGroupView>)groupedData;
        });
    }

    public Task<QueryResult<IList<FailedMessageView>>> GetGroupErrors(string groupId, string status, string modified, SortInfo sortInfo, PagingInfo pagingInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            // Get messages filtered by PrimaryFailureGroupId at database level
            var allMessages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.PrimaryFailureGroupId == groupId)
                .ToListAsync();

            var matchingMessages = allMessages
                .Where(fm =>
                {
                    var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(fm.FailureGroupsJson) ?? [];
                    return groups.Any(g => g.Id == groupId);
                })
                .ToList();

            // Apply status filter if provided
            if (!string.IsNullOrEmpty(status))
            {
                var statusEnum = Enum.Parse<FailedMessageStatus>(status, true);
                matchingMessages = [.. matchingMessages.Where(fm => fm.Status == statusEnum)];
            }

            var totalCount = matchingMessages.Count;

            // Apply sorting (simplified - would need full sorting implementation)
            matchingMessages = [.. matchingMessages
                .OrderByDescending(fm => fm.LastProcessedAt)
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.Next)];

            var results = matchingMessages.Select(CreateFailedMessageView).ToList();

            return new QueryResult<IList<FailedMessageView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryStatsInfo> GetGroupErrorsCount(string groupId, string status, string modified)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var allMessages = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => fm.PrimaryFailureGroupId == groupId)
                .ToListAsync();

            var count = allMessages
                .Count(fm =>
                {
                    var groups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(fm.FailureGroupsJson) ?? [];
                    var hasGroup = groups.Any(g => g.Id == groupId);

                    if (!hasGroup)
                    {
                        return false;
                    }

                    if (!string.IsNullOrEmpty(status))
                    {
                        var statusEnum = Enum.Parse<FailedMessageStatus>(status, true);
                        return fm.Status == statusEnum;
                    }

                    return true;
                });

            return new QueryStatsInfo(count.ToString(), count, false);
        });
    }

    public async Task<QueryResult<IList<FailureGroupView>>> GetGroup(string groupId, string status, string modified)
    {
        // This appears to be similar to GetFailureGroupView but returns a list
        var singleResult = await GetFailureGroupView(groupId, status, modified);

        if (singleResult.Results == null)
        {
            return new QueryResult<IList<FailureGroupView>>([], new QueryStatsInfo("0", 0, false));
        }

        return new QueryResult<IList<FailureGroupView>>([singleResult.Results], singleResult.QueryStats);
    }

    public Task EditComment(string groupId, string comment)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var commentEntity = new GroupCommentEntity
            {
                Id = Guid.Parse(groupId),
                GroupId = groupId,
                Comment = comment
            };

            // Use EF's change tracking for upsert
            var existing = await dbContext.GroupComments.FindAsync(commentEntity.Id);
            if (existing == null)
            {
                dbContext.GroupComments.Add(commentEntity);
            }
            else
            {
                dbContext.GroupComments.Update(commentEntity);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    public Task DeleteComment(string groupId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var comment = await dbContext.GroupComments
                .FirstOrDefaultAsync(gc => gc.GroupId == groupId);

            if (comment != null)
            {
                dbContext.GroupComments.Remove(comment);
                await dbContext.SaveChangesAsync();
            }
        });
    }
}
