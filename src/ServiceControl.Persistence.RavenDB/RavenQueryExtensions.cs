namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Infrastructure;

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

        public static IQueryable<TSource> Paging<TSource>(this IOrderedQueryable<TSource> source, PagingInfo pagingInfo)
            => source
                .Skip(pagingInfo.Offset)
                .Take(pagingInfo.PageSize);

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

        public static IAsyncDocumentQuery<TSource> Paging<TSource>(this IAsyncDocumentQuery<TSource> source, PagingInfo pagingInfo)
        {
            var maxResultsPerPage = pagingInfo.PageSize;
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = pagingInfo.Page;
            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IAsyncDocumentQuery<TSource> Sort<TSource>(this IAsyncDocumentQuery<TSource> source, SortInfo sortInfo)
        {
            var descending = true;

            var direction = sortInfo.Direction;
            if (direction == "asc")
            {
                descending = false;
            }

            var sort = sortInfo.Sort;
            if (!AsyncDocumentQuerySortOptions.Contains(sort))
            {
                sort = "time_sent";
            }

            string keySelector = sort switch
            {
                "id" or "message_id" => "MessageId",
                "message_type" => "MessageType",
                "status" => "Status",
                "modified" => "LastModified",
                "time_of_failure" => "TimeOfFailure",
                _ => "TimeSent",
            };
            return source.AddOrder(keySelector, descending);
        }


        public static IAsyncDocumentQuery<T> FilterByStatusWhere<T>(this IAsyncDocumentQuery<T> source, string status)
        {
            if (status == null)
            {
                return source;
            }

            var filters = status.Replace(" ", string.Empty).Split(',');
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

            if (includes.Any())
            {
                source.WhereIn("Status", includes.Cast<object>());
            }

            foreach (var exclude in excludes)
            {
                source.WhereNotEquals("Status", exclude);
            }

            return source;
        }


        public static IAsyncDocumentQuery<T> FilterByLastModifiedRange<T>(this IAsyncDocumentQuery<T> source, string modified)
        {
            if (modified == null)
            {
                return source;
            }

            var filters = modified.Split(SplitChars, StringSplitOptions.None);
            if (filters.Length != 2)
            {
                throw new Exception("Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }

            try
            {
                var from = DateTime.Parse(filters[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var to = DateTime.Parse(filters[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                source.AndAlso();
                source.WhereBetween("LastModified", from.Ticks, to.Ticks);
            }
            catch (Exception)
            {
                throw new Exception("Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }

            return source;
        }

        public static IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> FilterBySentTimeRange(this IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> source, DateTimeRange range)
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

        public static IAsyncDocumentQuery<T> FilterByQueueAddress<T>(this IAsyncDocumentQuery<T> source, string queueAddress)
        {
            if (string.IsNullOrWhiteSpace(queueAddress))
            {
                return source;
            }

            source.AndAlso();
            source.WhereEquals("QueueAddress", queueAddress.ToLowerInvariant());

            return source;
        }

        static HashSet<string> AsyncDocumentQuerySortOptions =
        [
            "id",
            "message_id",
            "message_type",
            "time_sent",
            "status",
            "modified",
            "time_of_failure"
        ];

        static string[] SplitChars =
        {
            "..."
        };
    }
}