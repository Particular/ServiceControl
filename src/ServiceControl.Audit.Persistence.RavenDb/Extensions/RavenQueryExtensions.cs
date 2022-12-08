namespace ServiceControl.Audit.Persistence.RavenDb.Extensions
{
    using System;
    using System.Linq.Expressions;
    using Raven.Client.Linq;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;

    static class RavenQueryExtensions
    {
        public static IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> source, bool includeSystemMessages)
        {
            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
        }

        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, PagingInfo pagingInfo)
            => source.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize);

        public static IRavenQueryable<TSource> Sort<TSource>(this IRavenQueryable<TSource> source, SortInfo sortInfo)
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