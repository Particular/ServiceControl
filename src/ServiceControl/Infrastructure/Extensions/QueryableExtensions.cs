namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using CompositeViews.Messages;
    using MessageFailures;
    using Nancy;
    using Raven.Client;
    using Raven.Client.Linq;

    public static class QueryableExtensions
    {
        public static IDocumentQuery<TSource> Paging<TSource>(this IDocumentQuery<TSource> source, Request request)
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

        public static IDocumentQuery<TSource> Sort<TSource>(this IDocumentQuery<TSource> source, Request request)
        {
            var direction = "desc";
            var descending = true;

            if (request.Query.direction.HasValue)
            {
                direction = (string) request.Query.direction;
            }

            if (direction == "asc")
            {
                descending = false;
            }

            var sortOptions = new[]
            {
                "id", "message_id", "message_type", 
                "time_sent", "status", "modified", "time_of_failure"
            };

            var sort = "time_sent";
            string keySelector;

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
                    keySelector = "MessageId";
                    break;

                case "message_type":
                    keySelector = "MessageType";
                    break;

                case "status":
                    keySelector = "Status";
                    break;

                case "modified":
                    keySelector = "LastModified";
                    break;

                case "time_of_failure":
                    keySelector = "TimeOfFailure";
                    break;

                default:
                    keySelector = "TimeSent";
                    break;
            }

            return source.AddOrder(keySelector, descending);
        }

        public static IDocumentQuery<T> FilterByStatusWhere<T>(this IDocumentQuery<T> source, Request request)
        {
            string status = null;

            if ((bool)request.Query.status.HasValue)
            {
                status = (string)request.Query.status;
            }

            if (status == null)
            {
                return source;
            }

            var filters = status.Replace(" ", String.Empty).Split(',');
            var excludes = new List<int>();
            var includes = new List<int>();

            foreach (var filter in filters)
            {
                FailedMessageStatus failedMessageStatus;

                if (filter.StartsWith("-"))
                {
                    if (Enum.TryParse(filter.Substring(1), true, out failedMessageStatus))
                    {
                        excludes.Add((int)failedMessageStatus);
                    }
                    continue;
                }

                if (Enum.TryParse(filter, true, out failedMessageStatus))
                {
                    includes.Add((int)failedMessageStatus);
                }
            }

            var sb = new StringBuilder();

            sb.Append("((");
            if (includes.Count == 0)
            {
                sb.Append("*");
            }
            else
            {
                sb.Append(String.Join(" OR ", includes.ToArray()));
            }
            sb.Append(")");

            if (excludes.Count > 0)
            {
                sb.Append(" AND NOT (");
                sb.Append(String.Join(" OR ", excludes.ToArray()));
                sb.Append(")");
            }
            sb.Append(")");

            source.AndAlso();
            source.Where(string.Format("Status: {0}", sb));

            return source;
        }


        public static IDocumentQuery<T> FilterByLastModifiedRange<T>(this IDocumentQuery<T> source, Request request)
        {
            string modified = null;

            if ((bool) request.Query.modified.HasValue)
            {
                modified = (string) request.Query.modified;
            }

            if (modified == null)
            {
                return source;
            }

            var filters = modified.Split(new[]
            {
                "..."
            }, StringSplitOptions.None);

            if (filters.Length != 2)
            {
                throw new Exception("Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }
            try
            {
                var from = DateTime.Parse(filters[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var to = DateTime.Parse(filters[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                source.AndAlso();
                source.WhereBetweenOrEqual("LastModified", from.Ticks, to.Ticks);
            }
            catch (Exception)
            {
                throw new Exception("Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }

            return source;
        }

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

        public static IOrderedQueryable<TSource> Paging<TSource>(this IOrderedQueryable<TSource> source, Request request)
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

            return (IOrderedQueryable<TSource>)source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, Request request)
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