namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Indexes;
    using ServiceControl.Audit.Infrastructure;

    static class RavenQueryExtensions
    {
        public static IQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IQueryable<MessagesViewIndex.SortAndFilterOptions> source, bool includeSystemMessages)
        {
            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
        }

        public static IQueryable<MessagesViewIndex.SortAndFilterOptions> FilterBySentTimeRange(this IQueryable<MessagesViewIndex.SortAndFilterOptions> source, DateTimeRange range)
        {
            if (range == null)
            {
                return source;
            }

            if (range.From.HasValue)
            {
                source = source.Where(m => m.TimeSent >= range.From);
            }

            if (range.To.HasValue)
            {
                source = source.Where(m => m.TimeSent <= range.To);
            }

            return source;
        }

        public static IQueryable<TSource> Paging<TSource>(this IQueryable<TSource> source, PagingInfo pagingInfo)
            => source.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize);

        public static IQueryable<TSource> Sort<TSource>(this IQueryable<TSource> source, SortInfo sortInfo)
            where TSource : MessagesViewIndex.SortAndFilterOptions
        {
            Expression<Func<TSource, object>> keySelector = sortInfo.Sort switch
            {
                "id" or "message_id" => m => m.MessageId,
                "message_type" => m => m.MessageType,
                "critical_time" => m => m.CriticalTime,
                "delivery_time" => m => m.DeliveryTime,
                "processing_time" => m => m.ProcessingTime,
                "processed_at" => m => m.ProcessedAt,
                "status" => m => m.Status,
                _ => m => m.TimeSent,
            };

            if (sortInfo.Direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }
    }
}