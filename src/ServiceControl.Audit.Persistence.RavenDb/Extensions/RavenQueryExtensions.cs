namespace ServiceControl.Audit.Persistence.RavenDb.Extensions
{
    using System;
    using System.Linq.Expressions;
    using System.Net.Http;
    using Audit.Auditing.MessagesView;
    using Infrastructure;
    using Raven.Client.Linq;

    static class RavenQueryExtensions
    {
        public static IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> source, HttpRequestMessage request)
        {
            var includeSystemMessages = request.GetQueryStringValue("include_system_messages", false);

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