namespace ServiceControl.Audit.Persistence.RavenDb.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        public static IRavenQueryable<TSource> Sort<TSource>(this IRavenQueryable<TSource> source, HttpRequestMessage request,
            Expression<Func<TSource, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
            where TSource : MessagesViewIndex.SortAndFilterOptions
        {
            var direction = request.GetQueryStringValue("direction", defaultSortDirection);
            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }


            Expression<Func<TSource, object>> keySelector;
            var sort = request.GetQueryStringValue("sort", "time_sent");
            if (!RavenQueryableSortOptions.Contains(sort))
            {
                sort = "time_sent";
            }

            switch (sort)
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
                    if (defaultKeySelector == null)
                    {
                        keySelector = m => m.TimeSent;
                    }
                    else
                    {
                        keySelector = defaultKeySelector;
                    }

                    break;
            }

            if (direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }

        static HashSet<string> RavenQueryableSortOptions = new HashSet<string>
        {
            "processed_at",
            "id",
            "message_type",
            "time_sent",
            "critical_time",
            "delivery_time",
            "processing_time",
            "status",
            "message_id"
        };
    }
}