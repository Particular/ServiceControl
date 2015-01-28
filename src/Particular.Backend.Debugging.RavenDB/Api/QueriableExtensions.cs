namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Nancy;
    using Raven.Client.Linq;

    public static class QueriableExtensions
    {
        public static IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> source, Request request)
        {
            var includeSystemMessages = false;

            if ((bool)request.Query.include_system_messages.HasValue)
            {
                includeSystemMessages = (bool)request.Query.include_system_messages;
            }

            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
        }
     

        public static IRavenQueryable<TSource> Sort<TSource>(this IRavenQueryable<TSource> source, Request request,
            Expression<Func<TSource, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
            where TSource : MessagesViewIndex.SortAndFilterOptions
        {
            var direction = defaultSortDirection;

            if (request.Query.direction.HasValue)
            {
                direction = (string) request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sortOptions = new[]
            {
                "processed_at", "id", "message_type",
                "time_sent", "critical_time", "delivery_time", "processing_time",
                "status", "message_id"
            };

            var sort = "time_sent";
            Expression<Func<TSource, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string) request.Query.sort;
            }

            if (!Enumerable.Contains(sortOptions, sort))
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
    }
}