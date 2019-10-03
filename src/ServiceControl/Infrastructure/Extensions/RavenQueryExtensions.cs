namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Http;
    using System.Text;
    using CompositeViews.Messages;
    using MessageFailures;
    using Raven.Client;
    using Raven.Client.Linq;

    static class RavenQueryExtensions
    {
        public static IAsyncDocumentQuery<TSource> Paging<TSource>(this IAsyncDocumentQuery<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);
            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IAsyncDocumentQuery<TSource> Sort<TSource>(this IAsyncDocumentQuery<TSource> source, HttpRequestMessage request)
        {
            var descending = true;

            var direction = request.GetQueryStringValue("direction", "desc");
            if (direction == "asc")
            {
                descending = false;
            }

            string keySelector;

            var sort = request.GetQueryStringValue("sort", "time_sent");
            if (!AsyncDocumentQuerySortOptions.Contains(sort))
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


        public static IAsyncDocumentQuery<T> FilterByStatusWhere<T>(this IAsyncDocumentQuery<T> source, HttpRequestMessage request)
        {
            var status = request.GetQueryStringValue<string>("status");
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

            var sb = new StringBuilder();

            sb.Append("((");
            if (includes.Count == 0)
            {
                sb.Append("*");
            }
            else
            {
                sb.Append(string.Join(" OR ", includes.ToArray()));
            }

            sb.Append(")");

            if (excludes.Count > 0)
            {
                sb.Append(" AND NOT (");
                sb.Append(string.Join(" OR ", excludes.ToArray()));
                sb.Append(")");
            }

            sb.Append(")");

            source.AndAlso();
            source.Where($"Status: {sb}");

            return source;
        }


        public static IAsyncDocumentQuery<T> FilterByLastModifiedRange<T>(this IAsyncDocumentQuery<T> source, HttpRequestMessage request)
        {
            var modified = request.GetQueryStringValue<string>("modified");

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
                source.WhereBetweenOrEqual("LastModified", from.Ticks, to.Ticks);
            }
            catch (Exception)
            {
                throw new Exception("Invalid modified date range, dates need to be in ISO8601 format and it needs to be a range eg. 2016-03-11T00:27:15.474Z...2016-03-16T03:27:15.474Z");
            }

            return source;
        }

        public static IAsyncDocumentQuery<T> FilterByQueueAddress<T>(this IAsyncDocumentQuery<T> source, HttpRequestMessage request)
        {
            var queueAddress = request.GetQueryStringValue<string>("queueaddress");
            if (string.IsNullOrWhiteSpace(queueAddress))
            {
                return source;
            }

            source.AndAlso();
            source.WhereEquals("QueueAddress", queueAddress.ToLowerInvariant());

            return source;
        }

        public static IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.SortAndFilterOptions> source, HttpRequestMessage request)
        {
            var includeSystemMessages = request.GetQueryStringValue("include_system_messages", false);
            return !includeSystemMessages ? source.Where(m => !m.IsSystemMessage) : source;
        }

        public static IOrderedQueryable<TSource> Paging<TSource>(this IOrderedQueryable<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return (IOrderedQueryable<TSource>)source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);

            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

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

        static T GetQueryStringValue<T>(this HttpRequestMessage request, string key, T defaultValue = default)
        {
            Dictionary<string, string> queryStringDictionary;
            if (!request.Properties.TryGetValue("QueryStringAsDictionary", out var dictionaryAsObject))
            {
                queryStringDictionary = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                request.Properties["QueryStringAsDictionary"] = queryStringDictionary;
            }
            else
            {
                queryStringDictionary = (Dictionary<string, string>)dictionaryAsObject;
            }

            queryStringDictionary.TryGetValue(key, out var value);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        static HashSet<string> AsyncDocumentQuerySortOptions = new HashSet<string>
        {
            "id",
            "message_id",
            "message_type",
            "time_sent",
            "status",
            "modified",
            "time_of_failure"
        };

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

        static string[] SplitChars =
        {
            "..."
        };
    }
}