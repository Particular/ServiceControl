namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Http;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;

    static class RavenQueryExtensions
    {
                public static IQueryable<MessagesView> ToMessagesView(this IQueryable<MessagesViewIndex.Result> query)
                => query.OfType<ProcessedMessage>()
                    .Select(message => new MessagesView
                    {
                        Id = message.UniqueMessageId,
                        MessageId = message.MessageMetadata.MessageId,
                        MessageType = message.MessageMetadata.MessageType,
                        SendingEndpoint = message.MessageMetadata.SendingEndpoint,
                        ReceivingEndpoint = message.MessageMetadata.ReceivingEndpoint,
                        TimeSent = message.MessageMetadata.TimeSent,
                        ProcessedAt = message.ProcessedAt,
                        CriticalTime = message.MessageMetadata.CriticalTime,
                        ProcessingTime = message.MessageMetadata.ProcessingTime,
                        DeliveryTime = message.MessageMetadata.DeliveryTime,
                        IsSystemMessage = message.MessageMetadata.IsSystemMessage,
                        ConversationId = message.MessageMetadata.ConversationId,
                        Headers = message.Headers.ToArray(),
                        Status = message.MessageMetadata.IsRetried ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                        MessageIntent = message.MessageMetadata.MessageIntent,
                        BodyUrl = message.MessageMetadata.BodyUrl,
                        BodySize = message.MessageMetadata.ContentLength,
                        InvokedSagas = message.MessageMetadata.InvokedSagas,
                        OriginatesFromSaga = message.MessageMetadata.OriginatesFromSaga
                    })
                    .As<MessagesView>()
                    ;


        public static IRavenQueryable<MessagesViewIndex.Result> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.Result> source, HttpRequestMessage request)
        {
            var includeSystemMessages = request.GetQueryStringValue("include_system_messages", false);

            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
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
            where TSource : MessagesViewIndex.Result
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