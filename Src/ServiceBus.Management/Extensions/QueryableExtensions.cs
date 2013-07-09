namespace ServiceBus.Management.Extensions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Nancy;
    using RavenDB.Indexes;

    public static class QueryableExtensions
    {
        public static IQueryable<Messages_Sort.Result> IncludeSystemMessagesWhere(this IQueryable<Messages_Sort.Result> source, Request request)
        {
            bool includeSystemMessages = false;

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

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                         .Take(maxResultsPerPage);
        }

        public static IOrderedQueryable<TSource> Sort<TSource>(this IQueryable<TSource> source, Request request, Expression<Func<TSource, object>> defaultKeySelector = null, string defaultSortDirection = "desc") where TSource : CommonResult
        {
            var direction = defaultSortDirection;

            if (request.Query.direction.HasValue)
            {
                direction = (string)request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sortOptions = new[] { "time_of_failure", "id", "message_type", "time_sent", "critical_time", "processing_time" };
            var sort = "time_sent";
            Expression<Func<TSource, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string)request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "time_sent"; 
            }

            switch (sort)
            {
                case "id":
                    keySelector = m => m.Id;
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

                case "time_of_failure":
                    keySelector = m => m.TimeOfFailure;
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
        /*
        public static IOrderedQueryable<Conversations_Sorted.Result> Sort(this IQueryable<Conversations_Sorted.Result> source, Request request, Expression<Func<Conversations_Sorted.Result, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
        {
            var direction = defaultSortDirection;

            if (request.Query.direction.HasValue)
            {
                direction = (string)request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sortOptions = new[] { "time_of_failure", "id", "message_type", "time_sent", "critical_time", "processing_time" };
            var sort = "time_sent";
            Expression<Func<Conversations_Sorted.Result, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string)request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "time_sent"; 
            }

            switch (sort)
            {
                case "id":
                    keySelector = m => m.Id;
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

                case "time_of_failure":
                    keySelector = m => m.TimeOfFailure;
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

        public static IOrderedQueryable<Messages_Search.Result> Sort(this IQueryable<Messages_Search.Result> source, Request request, Expression<Func<Messages_Search.Result, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
        {
            var direction = defaultSortDirection;

            if (request.Query.direction.HasValue)
            {
                direction = (string)request.Query.direction;
            }

            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }

            var sortOptions = new[] { "time_of_failure", "id", "message_type", "time_sent", "critical_time", "processing_time" };
            var sort = "time_sent";
            Expression<Func<Messages_Search.Result, object>> keySelector;

            if (request.Query.sort.HasValue)
            {
                sort = (string)request.Query.sort;
            }

            if (!sortOptions.Contains(sort))
            {
                sort = "time_sent"; 
            }

            switch (sort)
            {
                case "id":
                    keySelector = m => m.Id;
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

                case "time_of_failure":
                    keySelector = m => m.TimeOfFailure;
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
         */
    }
}