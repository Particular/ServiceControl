namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CompositeViews.Messages;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Persistence.Infrastructure;

partial class ErrorMessageDataStore
{
    public Task<QueryResult<IList<MessagesView>>> GetAllMessages(PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages.AsQueryable();

            // Apply time range filter
            if (timeSentRange != null)
            {
                if (timeSentRange.From.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent >= timeSentRange.From);
                }
                if (timeSentRange.To.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent <= timeSentRange.To);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateMessagesView(entity)).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForEndpoint(string endpointName, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages, DateTimeRange? timeSentRange = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .Where(fm => fm.ReceivingEndpointName == endpointName);

            // Apply time range filter
            if (timeSentRange != null)
            {
                if (timeSentRange.From.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent >= timeSentRange.From);
                }
                if (timeSentRange.To.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent <= timeSentRange.To);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateMessagesView(entity)).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesByConversation(string conversationId, PagingInfo pagingInfo, SortInfo sortInfo, bool includeSystemMessages)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .Where(fm => fm.ConversationId == conversationId);

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();
            var results = entities.Select(CreateMessagesView).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryResult<IList<MessagesView>>> GetAllMessagesForSearch(string searchTerms, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages.AsQueryable();

            // Apply full-text search
            if (!string.IsNullOrWhiteSpace(searchTerms))
            {
                query = fullTextSearchProvider.ApplyFullTextSearch(query, searchTerms);
            }

            // Apply time range filter
            if (timeSentRange != null)
            {
                if (timeSentRange.From.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent >= timeSentRange.From);
                }
                if (timeSentRange.To.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent <= timeSentRange.To);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateMessagesView(entity)).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryResult<IList<MessagesView>>> SearchEndpointMessages(string endpointName, string searchKeyword, PagingInfo pagingInfo, SortInfo sortInfo, DateTimeRange? timeSentRange = null)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages
                .Where(fm => fm.ReceivingEndpointName == endpointName);

            // Apply full-text search
            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                query = fullTextSearchProvider.ApplyFullTextSearch(query, searchKeyword);
            }

            // Apply time range filter
            if (timeSentRange != null)
            {
                if (timeSentRange.From.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent >= timeSentRange.From);
                }
                if (timeSentRange.To.HasValue)
                {
                    query = query.Where(fm => fm.TimeSent <= timeSentRange.To);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateMessagesView(entity)).ToList();

            return new QueryResult<IList<MessagesView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryResult<IList<FailedMessageView>>> ErrorGet(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<FailedMessageStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(fm => fm.Status == statusEnum);
                }
            }

            // Apply queue address filter
            if (!string.IsNullOrWhiteSpace(queueAddress))
            {
                query = query.Where(fm => fm.QueueAddress == queueAddress);
            }

            // Apply modified date filter
            if (!string.IsNullOrWhiteSpace(modified))
            {
                if (DateTime.TryParse(modified, out var modifiedDate))
                {
                    query = query.Where(fm => fm.LastProcessedAt >= modifiedDate);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateFailedMessageView(entity)).ToList();

            return new QueryResult<IList<FailedMessageView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<QueryStatsInfo> ErrorsHead(string status, string modified, string queueAddress)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<FailedMessageStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(fm => fm.Status == statusEnum);
                }
            }

            // Apply queue address filter
            if (!string.IsNullOrWhiteSpace(queueAddress))
            {
                query = query.Where(fm => fm.QueueAddress == queueAddress);
            }

            // Apply modified date filter
            if (!string.IsNullOrWhiteSpace(modified))
            {
                if (DateTime.TryParse(modified, out var modifiedDate))
                {
                    query = query.Where(fm => fm.LastProcessedAt >= modifiedDate);
                }
            }

            var totalCount = await query.CountAsync();

            return new QueryStatsInfo(totalCount.ToString(), totalCount, false);
        });
    }

    public Task<QueryResult<IList<FailedMessageView>>> ErrorsByEndpointName(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var query = dbContext.FailedMessages.AsQueryable();

            // Apply endpoint filter
            query = query.Where(fm => fm.ReceivingEndpointName == endpointName);

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<FailedMessageStatus>(status, true, out var statusEnum))
                {
                    query = query.Where(fm => fm.Status == statusEnum);
                }
            }

            // Apply modified date filter
            if (!string.IsNullOrWhiteSpace(modified))
            {
                if (DateTime.TryParse(modified, out var modifiedDate))
                {
                    query = query.Where(fm => fm.LastProcessedAt >= modifiedDate);
                }
            }

            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, sortInfo);

            // Apply paging
            query = query.Skip(pagingInfo.Offset).Take(pagingInfo.Next);

            var entities = await query.AsNoTracking().ToListAsync();

            var results = entities.Select(entity => CreateFailedMessageView(entity)).ToList();

            return new QueryResult<IList<FailedMessageView>>(results, new QueryStatsInfo(totalCount.ToString(), totalCount, false));
        });
    }

    public Task<IDictionary<string, object>> ErrorsSummary()
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var endpointStats = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => !string.IsNullOrEmpty(fm.ReceivingEndpointName))
                .GroupBy(fm => fm.ReceivingEndpointName)
                .Select(g => new { Endpoint = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Endpoint!, x => (object)x.Count);

            var messageTypeStats = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => !string.IsNullOrEmpty(fm.MessageType))
                .GroupBy(fm => fm.MessageType)
                .Select(g => new { MessageType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MessageType!, x => (object)x.Count);

            var hostStats = await dbContext.FailedMessages
                .AsNoTracking()
                .Where(fm => !string.IsNullOrEmpty(fm.QueueAddress))
                .GroupBy(fm => fm.QueueAddress)
                .Select(g => new { Host = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Host!, x => (object)x.Count);

            return (IDictionary<string, object>)new Dictionary<string, object>
            {
                ["Endpoints"] = endpointStats,
                ["Message types"] = messageTypeStats,
                ["Hosts"] = hostStats
            };
        });
    }

    public Task<FailedMessageView> ErrorLastBy(string failedMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.FailedMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(fm => fm.Id == Guid.Parse(failedMessageId));

            if (entity == null)
            {
                return null!;
            }

            var processingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [];
            var lastAttempt = processingAttempts.LastOrDefault();

            if (lastAttempt == null)
            {
                return null!;
            }

            return new FailedMessageView
            {
                Id = entity.UniqueMessageId,
                MessageType = entity.MessageType,
                TimeSent = entity.TimeSent,
                IsSystemMessage = false, // Not stored in entity
                Exception = lastAttempt.FailureDetails?.Exception,
                MessageId = entity.MessageId,
                NumberOfProcessingAttempts = entity.NumberOfProcessingAttempts ?? 0,
                Status = entity.Status,
                SendingEndpoint = null, // Would need to deserialize from JSON
                ReceivingEndpoint = null, // Would need to deserialize from JSON
                QueueAddress = entity.QueueAddress,
                TimeOfFailure = lastAttempt.FailureDetails?.TimeOfFailure ?? DateTime.MinValue,
                LastModified = entity.LastProcessedAt ?? DateTime.MinValue,
                Edited = false, // Not implemented
                EditOf = null
            };
        });
    }

    public Task<FailedMessage> ErrorBy(string failedMessageId)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            var entity = await dbContext.FailedMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(fm => fm.Id == Guid.Parse(failedMessageId));

            if (entity == null)
            {
                return null!;
            }

            return new FailedMessage
            {
                Id = entity.Id.ToString(),
                UniqueMessageId = entity.UniqueMessageId,
                Status = entity.Status,
                ProcessingAttempts = JsonSerializer.Deserialize<List<FailedMessage.ProcessingAttempt>>(entity.ProcessingAttemptsJson, JsonSerializationOptions.Default) ?? [],
                FailureGroups = JsonSerializer.Deserialize<List<FailedMessage.FailureGroup>>(entity.FailureGroupsJson, JsonSerializationOptions.Default) ?? []
            };
        });
    }
}
