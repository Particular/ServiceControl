namespace ServiceControl.Audit.Persistence.RavenDb.Extensions
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

        public static IQueryable<TSource> Paging<TSource>(this IQueryable<TSource> source, PagingInfo pagingInfo)
            => source.Skip(pagingInfo.Offset).Take(pagingInfo.PageSize);

        public static IQueryable<TSource> Sort<TSource>(this IQueryable<TSource> source, SortInfo sortInfo)
            where TSource : MessagesViewIndex.SortAndFilterOptions
        {
            Expression<Func<TSource, object>> keySelector;
            switch (sortInfo.Sort)
            {
                case "id":
                case "message_id":
                    keySelector = m => m.MessageId;
                    break;

                case "message_type":
                    keySelector = m => m.MessageType;
                    break;

                case "critical_time":
                    keySelector = m => m.CriticalTime;
                    break;

                case "delivery_time":
                    keySelector = m => m.DeliveryTime;
                    break;

                case "processing_time":
                    keySelector = m => m.ProcessingTime;
                    break;

                case "processed_at":
                    keySelector = m => m.ProcessedAt;
                    break;

                case "status":
                    keySelector = m => m.Status;
                    break;

                default:
                    keySelector = m => m.TimeSent;
                    break;
            }

            if (sortInfo.Direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }
    }
}