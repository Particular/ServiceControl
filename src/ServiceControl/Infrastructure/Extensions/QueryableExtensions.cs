namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using CompositeViews.Messages;
    using Nancy;
    using Raven.Client.Linq;

    public static class QueryableExtensions
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
     
        public static IQueryable<TSource> Paging<TSource>(this IQueryable<TSource> source, Request request)
        {
            var maxResultsPerPage = 50;

            if (request.Query.per_page.HasValue)
            {
                maxResultsPerPage = request.Query.per_page;
            }

            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = 1;

            if (request.Query.page.HasValue)
            {
                page = request.Query.page;
            }

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1)*maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IOrderedQueryable<TSource> Sort<TSource>(this IQueryable<TSource> source, Request request,
            Expression<Func<TSource, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
            where TSource : MessagesView
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
                "time_sent", "critical_time", "processing_time",
                "status"
            };

            var sort = "time_sent";
            Expression<Func<TSource, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string) request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
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